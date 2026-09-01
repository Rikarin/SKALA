using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1052</c> — <c>x != null ? x.Y : null</c> is <c>x?.Y</c>.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This rewrite evaluates the receiver once where the original evaluated it twice, and that
///         is the whole of its risk.
///     </b> It is stated rather than hidden: on a property with a
///     non-idempotent getter the two programs differ, and the rule admits property paths anyway for
///     the reason <see cref="RewriteGuards.IsPlainNamePath" /> gives — excluding them would silence it
///     on <c>this.Items</c> and <c>Options.Map</c>, which is most of its value, and the two reads it
///     collapses are adjacent within one expression. What it does <em>not</em> admit is an invocation,
///     an indexer or an <c>await</c> anywhere in the receiver: those are the ones whose second
///     evaluation is visibly a second call.
///     <para>
///         ⚠ <b>The branch's type must be a reference type and must be the conditional's own type.</b>
///         <c>x != null ? x.Count : null</c> is a target-typed <c>int?</c> and <c>x?.Count</c> is an
///         <c>int?</c> built a different way; matching them would need to reason about the target type
///         the expression was converted to, and getting it wrong changes an overload.
///     </para>
///     <para>
///         ⚠ A receiver that is already the subject of a <c>?.</c> — <c>x != null ? x?.Y : null</c> —
///         is refused rather than rewritten, because appending the suffix to the receiver would
///         produce <c>x??.Y</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MergedConditionalAccessAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.MergedConditionalAccess);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MergedConditionalAccess);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        var (receiver, checksForNull) = NullTest(conditional.Condition);
        if (receiver is null) {
            return;
        }

        // `x != null ? x.Y : null` and `x == null ? null : x.Y` are the same program written twice.
        var access = checksForNull ? conditional.WhenFalse : conditional.WhenTrue;
        var empty = checksForNull ? conditional.WhenTrue : conditional.WhenFalse;
        if (!PatternSafety.Unwrap(empty).IsKind(SyntaxKind.NullLiteralExpression)
            || !RewriteGuards.IsPlainNamePath(receiver)) {
            return;
        }

        var (prefix, suffix) = Suffix(PatternSafety.Unwrap(access), receiver);
        if (prefix is null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var receiverType = model.GetTypeInfo(receiver, cancellation).Type;
        if (receiverType is null
            || receiverType.TypeKind is TypeKind.Error or TypeKind.Dynamic
            || !receiverType.IsReferenceType
            || !NullComparison.IsRewritable(model, receiver, cancellation)) {
            return;
        }

        // ⚠ `x?.Count` on an `int` member is `int?`, so only a reference-typed member keeps the type
        // the conditional already had. Anything else is a different expression that happens to be
        // convertible to the same place.
        var accessType = model.GetTypeInfo(access, cancellation).Type;
        var conditionalType = model.GetTypeInfo(conditional, cancellation).Type;
        if (accessType is null
            || !accessType.IsReferenceType
            || conditionalType is null
            || !SymbolEqualityComparer.Default.Equals(accessType, conditionalType)) {
            return;
        }

        // `?.` is CS8072 inside an expression tree.
        if (NullComparison.InsideExpressionTree(model, conditional, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(conditional)
            || RewriteGuards.ContainsCommentOrDirective(conditional.SyntaxTree, conditional.FullSpan)) {
            return;
        }

        var replacement = prefix + "?" + suffix;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                conditional.GetLocation(),
                FixEdits.Pack((conditional.Span, replacement)),
                "The conditional is a conditional access: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>
    ///     The operand of a null test, and whether the test asks for null rather than for non-null.
    /// </summary>
    static (ExpressionSyntax? Operand, bool ChecksForNull) NullTest(ExpressionSyntax condition) {
        var current = PatternSafety.Unwrap(condition);
        if (current is BinaryExpressionSyntax comparison
            && NullComparison.OperandOf(comparison) is { } operand) {
            return (operand, comparison.IsKind(SyntaxKind.EqualsExpression));
        }

        if (current is IsPatternExpressionSyntax test) {
            return test.Pattern switch {
                ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression } =>
                    (test.Expression, true),
                UnaryPatternSyntax {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                } => (test.Expression, false),
                _ => (null, false)
            };
        }

        return (null, false);
    }

    /// <summary>
    ///     The receiver an access chain starts from and the text that follows it, or nulls.
    /// </summary>
    /// <remarks>
    ///     ⚠ The chain is walked through member accesses, element accesses and invocations only. A
    ///     <c>ConditionalAccessExpression</c> is deliberately not walked: <c>x != null ? x?.Y : null</c>
    ///     would otherwise splice into <c>x??.Y</c>.
    /// </remarks>
    static (ExpressionSyntax? Prefix, string Suffix) Suffix(ExpressionSyntax access, ExpressionSyntax receiver) {
        var current = access;
        while (true) {
            var next = current switch {
                MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } member =>
                    member.Expression,
                ElementAccessExpressionSyntax element => element.Expression,
                InvocationExpressionSyntax invocation => invocation.Expression,
                _ => null
            };

            if (next is null) {
                return (null, string.Empty);
            }

            if (RewriteGuards.Same(next, receiver)) {
                var text = access.SyntaxTree.GetText()
                    .ToString(TextSpan.FromBounds(next.Span.End, access.Span.End));

                // `x()` would splice into `x?()`. Only a member or an element access can follow a `?`.
                return text.StartsWith(".", System.StringComparison.Ordinal)
                    || text.StartsWith("[", System.StringComparison.Ordinal)
                        ? (next, text)
                        : (null, string.Empty);
            }

            current = next;
        }
    }
}
