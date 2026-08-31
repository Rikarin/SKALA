using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1031</c> — <c>if (x is not null) x.P = v;</c> is <c>x?.P = v;</c> in C# 14.
/// </summary>
/// <remarks>
///     ⚠ The rewrite is exact, and it is exact for a reason that is easy to assume rather than check:
///     C# 14's null-conditional assignment <b>does not evaluate the right-hand side</b> when the
///     receiver is null. Were that not so this would be a different program whenever <c>v</c> has a side
///     effect, and the rule would be wrong in precisely the cases that matter most.
///     <para>
///         ⚠ It only fires on a receiver that is a chain of plain names. The original evaluates the receiver
///         twice — once in the test, once in the assignment — and <c>x?.P = v</c> evaluates it once; on
///         anything with a side effect that is a change, and on a name it is free.
///     </para>
///     <para>
///         ⚠ <c>x != null</c> is admitted only where the operand's type declares no <c>operator ==</c>, the
///         same proof <c>SK1010</c> and <c>SK1020</c> make: <c>?.</c> tests for null, and a user-defined
///         operator can mean anything at all.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullConditionalAssignmentAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NullConditionalAssignment);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullConditionalAssignment);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (IfStatementSyntax)context.Node;
        if (statement.Else is not null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var guarded = NonNullOperand(model, statement.Condition, cancellation);
        if (guarded is null || !RewriteGuards.IsPlainNamePath(guarded)) {
            return;
        }

        var body = statement.Statement switch {
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0],
            ExpressionStatementSyntax bare => bare,
            _ => null
        };

        if (body is not ExpressionStatementSyntax {
                Expression:
                AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
            }) {
            return;
        }

        // The target must be a member access whose innermost receiver is the tested name.
        if (assignment.Left is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } target) {
            return;
        }

        var receiver = MatchInChain(target, guarded);
        if (receiver is null) {
            return;
        }

        // ⚠ `?.` needs a receiver that can be null. A non-nullable value type never reaches here
        // because `x is not null` on one does not compile, but a type parameter can, and
        // `T?.P` is not legal for an unconstrained `T`.
        var type = model.GetTypeInfo(receiver, cancellation).Type;
        if (type is null || type.TypeKind is TypeKind.Error or TypeKind.Dynamic || !type.IsReferenceType) {
            return;
        }

        // ⚠ A null-conditional assignment is a conditional evaluation, and neither it nor the
        // statement shape it replaces exists inside an expression tree.
        if (NullComparison.InsideExpressionTree(model, statement, cancellation)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(statement)) {
            return;
        }

        var text = assignment.ToString();
        var insertion = receiver.Span.End - assignment.SpanStart;
        if (insertion <= 0 || insertion >= text.Length) {
            return;
        }

        var replacement = text.Substring(0, insertion) + "?" + text.Substring(insertion) + ";";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, statement.Span),
                FixEdits.Pack((statement.Span, replacement)),
                "A null guard around one assignment is a null-conditional assignment: `"
                + RewriteGuards.Trim(replacement)
                + "`"
            )
        );
    }

    /// <summary>The operand a condition proves non-null, or null when it proves nothing.</summary>
    static ExpressionSyntax? NonNullOperand(
        SemanticModel model,
        ExpressionSyntax condition,
        System.Threading.CancellationToken cancellation
    ) {
        while (condition is ParenthesizedExpressionSyntax parenthesized) {
            condition = parenthesized.Expression;
        }

        switch (condition) {
            // `x is not null`
            case IsPatternExpressionSyntax {
                Pattern:
                UnaryPatternSyntax {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                }
            } pattern:
                return pattern.Expression;

            // `x != null`, where the type's own `operator !=` does not make that a different test.
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } binary: {
                var operand = NullComparison.OperandOf(binary);
                return operand is not null && NullComparison.IsRewritable(model, operand, cancellation)
                    ? operand
                    : null;
            }

            default:
                return null;
        }
    }

    /// <summary>
    ///     The link of the target's member-access chain that the condition proved non-null.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not simply the leftmost link. <c>if (a.B is not null) a.B.C = v;</c> is guarded on
    ///     <c>a.B</c>, and <c>a?.B.C = v</c> would be a different program — it would leave the
    ///     assignment happening when <c>a.B</c> is null, which is the NullReferenceException the
    ///     original was written to avoid. The <c>?</c> goes exactly where the test was.
    /// </remarks>
    static ExpressionSyntax? MatchInChain(MemberAccessExpressionSyntax target, ExpressionSyntax guarded) {
        var current = target.Expression;
        while (true) {
            if (RewriteGuards.Same(current, guarded)) {
                return current;
            }

            if (current is not MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } member) {
                return null;
            }

            current = member.Expression;
        }
    }
}
