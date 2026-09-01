using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3042</c> — the double-checked locking idiom is written over a field that is not
///     <c>volatile</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     The idiom exists to skip the lock on the common path, and the read it skips the lock for is the
///     <em>outer</em> one. Without <c>volatile</c> nothing orders that read against the writes the
///     constructor performed, so a second thread may see a non-null reference to an object whose
///     fields it cannot see yet and use it. The lock is real, the inner check is real, and the object
///     is still half-built.
///     <para>
///         ⚠ It works on x86 and x64 by accident of those processors' ordering, and stops working on
///         ARM64 — which is where a great deal of .NET now runs. That is why this is a finding rather
///         than a style note: the shape is not merely fragile, it has a platform on which it is
///         already wrong.
///     </para>
///     <para>
///         ⚠ Complements <c>SK3009</c>, which reports the <c>Lazy&lt;T&gt;</c> alternative being
///         constructed without thread safety. Neither fires where the other does: this rule needs a
///         hand-written check-lock-check and <c>SK3009</c> needs a <c>Lazy&lt;T&gt;</c> field.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DoubleCheckedLockingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IncorrectDoubleCheckedLocking);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var outer = (IfStatementSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The whole condition, never a conjunct of one. `if (instance == null && ready)` is a
        // different program: the outer fast path is guarded by something else as well, and whether
        // that something else orders the read is not decidable from this shape.
        if (CheckedField(outer.Condition, model, cancellation) is not { } field) {
            return;
        }

        // ⚠ Reference types only. The advice this finding carries is "make the field `volatile`",
        // and CS0677 makes that illegal for `long`, `double`, `decimal` and any user struct — so on
        // those the message would name a repair that does not compile. `bool` is a legal volatile
        // type and is the flag spelling of the same idiom, which is why it is admitted below.
        if (!field.Type.IsReferenceType && field.Type.SpecialType != SpecialType.System_Boolean) {
            return;
        }

        // ⚠ Declared in source. A field from another assembly cannot be made `volatile` by anyone
        // reading this finding, so reporting it is noise with no action attached.
        if (field.IsVolatile || field.DeclaringSyntaxReferences.Length == 0) {
            return;
        }

        if (FindLock(outer.Statement) is not { } locked
            || FindInnerCheck(locked.Statement, field, model, cancellation) is not { } inner
            || !AssignsField(inner.Statement, field, model, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                outer.Condition.GetLocation(),
                "the double-checked locking over `"
                + field.Name
                + "` reads it outside the lock and the field is not `volatile`, so a second thread can see the reference before the object it points at"
            )
        );
    }

    /// <summary>
    ///     The field an "is it initialized yet" condition tests, or <c>null</c> if that is not what the
    ///     condition is.
    /// </summary>
    /// <remarks>
    ///     Three spellings and no more: <c>f == null</c> either way round, <c>f is null</c>, and
    ///     <c>!f</c> on a <c>bool</c> field. ⚠ <c>Volatile.Read(ref f) == null</c> is deliberately not
    ///     one of them — that code has already done the ordering by hand and the rule must stay silent
    ///     on it, which it does for free by requiring the operand to be a plain field reference.
    /// </remarks>
    static IFieldSymbol? CheckedField(ExpressionSyntax condition, SemanticModel model, CancellationToken cancellation) {
        switch (condition) {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
                if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression)) {
                    return Field(binary.Left, model, cancellation);
                }

                return binary.Left.IsKind(SyntaxKind.NullLiteralExpression)
                    ? Field(binary.Right, model, cancellation)
                    : null;

            case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constant } pattern
                when constant.Expression.IsKind(SyntaxKind.NullLiteralExpression):
                return Field(pattern.Expression, model, cancellation);

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
                return Field(unary.Operand, model, cancellation) is {
                    Type.SpecialType: SpecialType.System_Boolean
                } flag
                        ? flag
                        : null;

            default:
                return null;
        }
    }

    static IFieldSymbol? Field(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellation) =>
        expression is IdentifierNameSyntax or MemberAccessExpressionSyntax
            ? model.GetSymbolInfo(expression, cancellation).Symbol as IFieldSymbol
            : null;

    /// <summary>The <c>lock</c> the outer check's body runs, if the body is one.</summary>
    static LockStatementSyntax? FindLock(StatementSyntax body) {
        if (body is LockStatementSyntax direct) {
            return direct;
        }

        if (body is not BlockSyntax block) {
            return null;
        }

        foreach (var statement in block.Statements) {
            if (statement is LockStatementSyntax found) {
                return found;
            }
        }

        return null;
    }

    /// <summary>The second check, which must test the same field as the first.</summary>
    static IfStatementSyntax? FindInnerCheck(
        StatementSyntax body,
        IFieldSymbol field,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        foreach (var statement in Statements(body)) {
            if (statement is IfStatementSyntax candidate
                && CheckedField(candidate.Condition, model, cancellation) is { } checkedField
                && SymbolEqualityComparer.Default.Equals(checkedField, field)) {
                return candidate;
            }
        }

        return null;
    }

    static IEnumerable<StatementSyntax> Statements(StatementSyntax body) {
        if (body is BlockSyntax block) {
            return block.Statements;
        }

        return new[] { body };
    }

    /// <summary>
    ///     Whether the inner branch actually publishes the field with a plain assignment.
    /// </summary>
    /// <remarks>
    ///     ⚠ The last gate, and the one that keeps <c>Interlocked.CompareExchange(ref f, …)</c> out.
    ///     That publication is already ordered, it is not a plain assignment, and a finding on it
    ///     would be advice to add <c>volatile</c> to code that does not need it.
    /// </remarks>
    static bool AssignsField(
        StatementSyntax body,
        IFieldSymbol field,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        foreach (var assignment in body.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>()) {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && Field(assignment.Left, model, cancellation) is { } assigned
                && SymbolEqualityComparer.Default.Equals(assigned, field)) {
                return true;
            }
        }

        return false;
    }
}
