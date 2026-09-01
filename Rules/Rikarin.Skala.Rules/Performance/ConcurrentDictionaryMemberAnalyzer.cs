using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4033</c> — the <c>ConcurrentDictionary</c> member taken is the expensive one.
/// </summary>
/// <remarks>
///     <para>
///         Three of this type's members are ordinary property reads on every other dictionary and are
///         not on this one. <c>Keys</c> and <c>Values</c> acquire every lock in the table and copy the
///         whole collection into a fresh <c>List</c> before the caller reads anything at all, so
///         <c>dict.Keys.Count</c> is a locking O(n) allocation for a number the table already knows.
///         <c>Count</c> acquires every lock too. <c>IsEmpty</c> acquires none and allocates nothing.
///     </para>
///     <para>
///         ⚠ <c>SK1034</c> reads <c>dict.Keys.Count()</c> and offers <c>dict.Keys.Count</c>, which is
///         correct and still leaves the expensive half in place. This rule declares
///         <c>supersedes: ["SK1034"]</c> and reports on the same span, so where both fire the stronger
///         remedy wins and the weaker one stays in the report marked superseded. Matching the span is
///         the whole mechanism — <c>Supersession.Apply</c> pairs findings by (rule, file, line,
///         column) — which is why <c>SK1034</c> runs in this batch's own test list.
///     </para>
///     <para>
///         ⚠ <c>dict.Keys.Contains(k)</c> looks like it belongs here and does not.
///         <c>Keys</c> hands back a plain <c>List&lt;TKey&gt;</c>, whose <c>Contains</c> uses
///         <c>EqualityComparer&lt;TKey&gt;.Default</c>, while <c>ContainsKey</c> uses the comparer the
///         dictionary was <em>constructed</em> with. For a table built with
///         <c>StringComparer.OrdinalIgnoreCase</c> the two give different answers, and nothing visible
///         at the call site says which one this is.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcurrentDictionaryMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.ConcurrentDictionaryExpensiveMember);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var dictionary =
                    start.Compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2");

                // ⚠ `IsEmpty` is the entire remedy, so it is resolved rather than assumed. The
                // analyzer targets netstandard2.0 and runs against whatever the project targets.
                if (dictionary is null || !HasIsEmpty(dictionary)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Comparison(context, dictionary),
                    SyntaxKind.EqualsExpression,
                    SyntaxKind.NotEqualsExpression,
                    SyntaxKind.GreaterThanExpression,
                    SyntaxKind.GreaterThanOrEqualExpression,
                    SyntaxKind.LessThanExpression,
                    SyntaxKind.LessThanOrEqualExpression
                );

                start.RegisterSyntaxNodeAction(
                    context => Property(context, dictionary),
                    SyntaxKind.SimpleMemberAccessExpression
                );

                start.RegisterSyntaxNodeAction(
                    context => Call(context, dictionary),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static bool HasIsEmpty(INamedTypeSymbol dictionary) {
        foreach (var member in dictionary.GetMembers("IsEmpty")) {
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } property
                && property.Type.SpecialType == SpecialType.System_Boolean) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     <c>dict.Count == 0</c> and its eleven relatives are <c>dict.IsEmpty</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The count expression here may itself be one of the shapes the other two handlers report,
    ///     so <c>dict.Keys.Count() &gt; 0</c> lands as one finding carrying the whole rewrite rather
    ///     than as two findings whose fixes have to be applied in order.
    /// </remarks>
    static void Comparison(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionary) {
        var comparison = (BinaryExpressionSyntax)context.Node;
        var kind = (SyntaxKind)comparison.RawKind;

        ExpressionSyntax? receiver;
        var bound = 0;
        if ((receiver = Counted(context, comparison.Left, dictionary)) is not null
            && IsConstant(comparison.Right, out bound)) {
            // The written order is already `count OP constant`.
        } else if ((receiver = Counted(context, comparison.Right, dictionary)) is not null
                   && IsConstant(comparison.Left, out bound)) {
            kind = Mirror(kind);
        } else {
            return;
        }

        // ⚠ A count is never negative, so `<= 0` is `== 0` and `>= 1` is `> 0`. Nothing else about
        // a count is decidable from a comparison with a constant, so nothing else is reported.
        var empty = (kind, bound) switch {
            (SyntaxKind.EqualsExpression, 0) => true,
            (SyntaxKind.LessThanOrEqualExpression, 0) => true,
            (SyntaxKind.LessThanExpression, 1) => true,
            (SyntaxKind.NotEqualsExpression, 0) => false,
            (SyntaxKind.GreaterThanExpression, 0) => false,
            (SyntaxKind.GreaterThanOrEqualExpression, 1) => false,
            _ => (bool?)null
        };

        if (empty is null || CallShape.ContainsComment(comparison)) {
            return;
        }

        // Both replacements are at least as tightly binding as the comparison they replace, so no
        // context that accepted the comparison can reject them.
        var replacement = (empty.Value ? string.Empty : "!") + receiver + ".IsEmpty";
        Report(
            context,
            comparison.Span,
            replacement,
            "`Count` locks the whole table; `IsEmpty` answers this without taking a lock"
        );
    }

    /// <summary><c>dict.Keys.Count</c> is <c>dict.Count</c>: the same number without the copy.</summary>
    static void Property(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionary) {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name.Identifier.ValueText != "Count"
            || access.Parent is InvocationExpressionSyntax invocation
            && ReferenceEquals(invocation.Expression, access)
            || IsComparedWithAConstant(access)
            || Snapshot(context, access.Expression, dictionary) is not { } receiver) {
            return;
        }

        Report(context, access.Span, receiver + ".Count", Copied(access));
    }

    /// <summary><c>dict.Keys.Count()</c> and <c>dict.Values.Any()</c>.</summary>
    static void Call(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionary) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (name is not ("Count" or "Any")
            || Snapshot(context, access.Expression, dictionary) is not { } receiver) {
            return;
        }

        if (name == "Count") {
            if (IsComparedWithAConstant(invocation)) {
                return;
            }

            Report(context, invocation.Span, receiver + ".Count", Copied(access));
            return;
        }

        // ⚠ `!dict.Keys.Any()` is `dict.IsEmpty` and the span reported is the negation's, which is
        // also the span SK1034 reports for it. The supersession pairs on position.
        if (invocation.Parent is PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.LogicalNotExpression
            } negation) {
            Report(context, negation.Span, receiver + ".IsEmpty", Copied(access));
            return;
        }

        // ⚠ `!x` is a unary expression where the invocation was a primary one, so it may only
        // replace it where the surrounding syntax cannot re-bind. `dict.Keys.Any().ToString()`
        // would become `!dict.IsEmpty.ToString()`, which parses, binds, and means something else.
        if (IsSafeBooleanPosition(invocation)) {
            Report(context, invocation.Span, "!" + receiver + ".IsEmpty", Copied(access));
        }
    }

    static string Copied(MemberAccessExpressionSyntax access) =>
        "`"
        + (access.Expression is MemberAccessExpressionSyntax inner ? inner.Name.Identifier.ValueText : "Keys")
        + "` takes every lock in the table and copies the whole collection before this is read";

    static void Report(SyntaxNodeAnalysisContext context, TextSpan span, string replacement, string message) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );

    /// <summary>
    ///     The dictionary behind <c>dict.Keys</c> or <c>dict.Values</c>, when there is one.
    /// </summary>
    static ExpressionSyntax? Snapshot(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol dictionary
    ) {
        if (expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Name.Identifier.ValueText is not ("Keys" or "Values")
            || !CallShape.IsPlainNamePath(access.Expression)) {
            return null;
        }

        var type = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        return type?.TypeKind != TypeKind.Error && CallShape.Is(type, dictionary) ? access.Expression : null;
    }

    /// <summary>
    ///     The dictionary behind any expression that yields its count — <c>dict.Count</c>,
    ///     <c>dict.Keys.Count</c>, <c>dict.Values.Count()</c> and the rest.
    /// </summary>
    static ExpressionSyntax? Counted(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol dictionary
    ) {
        while (expression is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses.Expression;
        }

        if (expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation) {
            expression = invocation.Expression;
        }

        if (expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "Count"
            } access) {
            return null;
        }

        if (Snapshot(context, access.Expression, dictionary) is { } through) {
            return through;
        }

        if (!CallShape.IsPlainNamePath(access.Expression)) {
            return null;
        }

        var type = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        return type?.TypeKind != TypeKind.Error && CallShape.Is(type, dictionary) ? access.Expression : null;
    }

    /// <summary>Whether this expression is one side of a comparison with an integer literal.</summary>
    static bool IsComparedWithAConstant(ExpressionSyntax expression) {
        SyntaxNode node = expression;
        while (node.Parent is ParenthesizedExpressionSyntax parentheses) {
            node = parentheses;
        }

        return node.Parent is BinaryExpressionSyntax comparison
            && IsConstant(ReferenceEquals(comparison.Left, node) ? comparison.Right : comparison.Left, out _);
    }

    static SyntaxKind Mirror(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind
        };

    static bool IsConstant(ExpressionSyntax expression, out int value) {
        value = 0;
        if (expression is not LiteralExpressionSyntax { Token.Value: int literal }) {
            return false;
        }

        value = literal;
        return true;
    }

    /// <summary>
    ///     ⚠ Where a unary expression may replace a primary one without re-binding — the same list
    ///     <c>SK1034</c> uses, and for the same reason.
    /// </summary>
    static bool IsSafeBooleanPosition(InvocationExpressionSyntax invocation) =>
        invocation.Parent switch {
            IfStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            WhileStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            DoStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            ConditionalExpressionSyntax conditional => ReferenceEquals(conditional.Condition, invocation),
            BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.LogicalAndExpression or (int)SyntaxKind.LogicalOrExpression
            } => true,
            ParenthesizedExpressionSyntax => true,
            ReturnStatementSyntax => true,
            ArrowExpressionClauseSyntax => true,
            ArgumentSyntax => true,
            _ => false
        };
}
