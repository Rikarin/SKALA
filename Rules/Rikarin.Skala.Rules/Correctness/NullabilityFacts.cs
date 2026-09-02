using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The three questions the <c>SK2110</c>–<c>SK2113</c> nullability rules share.
/// </summary>
/// <remarks>
///     ⚠ <b>Annotation and flow state are different questions and confusing them produces a rule that
///     reports nothing at all.</b> <c>GetTypeInfo</c> on a written <see cref="TypeSyntax" /> answers
///     <see cref="NullableAnnotation.None" /> for <c>string</c> and for <c>string?</c> alike — the
///     annotation of a *type reference* is not the annotation of the *symbol* it names — so a rule that
///     compares annotations there is silent on every input and looks exactly like a rule with nothing
///     to find. What the rules here ask is <see cref="NullableFlowState" />: what the compiler knows at
///     that point in the program.
///     <para>
///         ⚠ <b>Both answers are worthless in a nullable-oblivious context and are not <c>false</c>
///         there.</b> Under <c>#nullable disable</c> every expression's flow state is
///         <see cref="NullableFlowState.None" />, which is neither <c>MaybeNull</c> nor <c>NotNull</c>.
///         A rule that tests <c>!= MaybeNull</c> silently treats the whole nullable-oblivious world as
///         proven non-null; a rule that tests <c>== NotNull</c> withdraws from it. Which of those is
///         right depends on the rule, so this type exposes the context itself rather than a verdict.
///     </para>
/// </remarks>
internal static class NullabilityFacts {
    /// <summary>Whether the compiler could issue a nullable *warning* at this position.</summary>
    /// <remarks>
    ///     ⚠ Warnings and annotations are enabled separately — <c>#nullable disable warnings</c> leaves
    ///     <c>?</c> meaningful and every diagnostic off — so the two are asked for separately. This is
    ///     the one that decides whether a <c>!</c> could have been suppressing anything.
    /// </remarks>
    public static bool WarningsEnabledAt(SemanticModel model, int position) =>
        (model.GetNullableContext(position) & NullableContext.WarningsEnabled) != 0;

    /// <summary>Whether a <c>?</c> written at this position means anything.</summary>
    public static bool AnnotationsEnabledAt(SemanticModel model, int position) =>
        (model.GetNullableContext(position) & NullableContext.AnnotationsEnabled) != 0;

    /// <summary>Whether the compiler has *proved* the expression non-null at this point.</summary>
    /// <remarks>
    ///     ⚠ Deliberately <c>== NotNull</c> rather than <c>!= MaybeNull</c>. The third state,
    ///     <see cref="NullableFlowState.None" />, means the compiler was not asked — every expression in
    ///     a nullable-oblivious file has it — and a rule that removes an annotation on the strength of
    ///     it is acting on an absence of information.
    /// </remarks>
    public static bool IsProvenNotNull(SemanticModel model, ExpressionSyntax expression, CancellationToken token) =>
        model.GetTypeInfo(expression, token).Nullability.FlowState == NullableFlowState.NotNull;

    /// <summary>Whether the compiler already knows this expression's value is null.</summary>
    /// <remarks>
    ///     <c>null</c>, <c>default</c>, <c>default(string)</c> and <c>(string)null</c> all answer yes,
    ///     and they answer it in every nullable context because a constant is not flow analysis. This is
    ///     what lets <c>SK2110</c> work in the nullable-oblivious files that are its whole reason to
    ///     exist.
    /// </remarks>
    public static bool IsConstantNull(SemanticModel model, ExpressionSyntax expression, CancellationToken token) {
        var constant = model.GetConstantValue(expression, token);
        return constant.HasValue && constant.Value is null;
    }

    /// <summary>
    ///     Every node under <paramref name="root" /> that belongs to the same function.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>return null;</c> inside a lambda is the *lambda's* return, and a rule that walked
    ///     <c>DescendantNodes()</c> would attribute it to the enclosing method. The same walk is what
    ///     keeps <c>SK2112</c> honest in the other direction: an assignment to a local from inside a
    ///     lambda *is* the enclosing method's assignment, so that rule uses the whole subtree instead
    ///     and the two callers ask for what they mean.
    /// </remarks>
    public static IEnumerable<SyntaxNode> DescendantsWithinTheSameFunction(SyntaxNode root) {
        var pending = new Stack<SyntaxNode>();
        foreach (var child in root.ChildNodes()) {
            pending.Push(child);
        }

        while (pending.Count > 0) {
            var node = pending.Pop();
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                continue;
            }

            yield return node;
            foreach (var child in node.ChildNodes()) {
                pending.Push(child);
            }
        }
    }
}
