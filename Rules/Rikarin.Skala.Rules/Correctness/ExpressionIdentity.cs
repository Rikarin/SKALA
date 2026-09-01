using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The two questions the "reads as something it is not" family shares: are these the same
///     expression, and can the answer be read twice without the program noticing.
/// </summary>
/// <remarks>
///     ⚠ <b>Structural equality is Roslyn's, not one written here.</b>
///     <see cref="SyntaxFactory.AreEquivalent(SyntaxNode?, SyntaxNode?, bool)" /> with
///     <c>topLevel: false</c> compares tokens and structure and ignores trivia, which is exactly the
///     comparison <c>SK2061</c> and <c>SK2062</c> need — and it is the comparison a hand-written one
///     gets subtly wrong. A <em>text</em> comparison would call <c>a &amp;&amp; b</c> and
///     <c>a  &amp;&amp;  b</c> different, and would tempt a rule into comparing sub-expressions, which
///     is the false positive doc 08 names for this family: two conditions over different subjects that
///     happen to share a term are not the same condition.
///     <para>
///         ⚠ <b>Equality alone is never enough.</b> <c>Next() == Next()</c> is the same expression twice
///         and two different draws; <c>if (Read()) … else if (Read())</c> is two different questions. So
///         every rule here pairs the structural test with <see cref="IsRepeatable" />, which is a
///         syntactic over-approximation of "evaluating this a second time, with nothing in between,
///         cannot be observed".
///     </para>
/// </remarks>
internal static class ExpressionIdentity {
    /// <summary>Whether two expressions are the same tree, ignoring layout and comments.</summary>
    public static bool Same(ExpressionSyntax left, ExpressionSyntax right) =>
        SyntaxFactory.AreEquivalent(left, right, false);

    /// <summary>
    ///     Whether an expression can be evaluated twice, back to back, without the program noticing.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately syntactic and deliberately pessimistic: an invocation, an object creation, an
    ///     <c>await</c>, an assignment, an increment, an element access or a <c>ref</c>/<c>out</c>
    ///     argument anywhere inside withdraws the answer. An indexer is a call and a
    ///     <c>stackalloc</c> is an allocation, so both count as effects. Everything that survives is
    ///     built from names, member-access paths, literals, casts, patterns and operators over them.
    ///     <para>
    ///         ⚠ A property read <em>is</em> a method call and is admitted anyway, for the reason
    ///         <c>RewriteGuards.IsPlainNamePath</c> gives: excluding it would silence these rules on
    ///         <c>x.Count</c> and <c>Options.Mode</c>, which is most of what real conditions are made
    ///         of, and nothing at all runs between the two reads these rules compare. Callers that
    ///         cannot afford even that — <c>SK2061</c>, which has no "nothing ran in between" argument
    ///         because the two operands are evaluated as part of one expression whose operator may be
    ///         anything — use <see cref="IsStableDataPath" /> instead.
    ///     </para>
    /// </remarks>
    public static bool IsRepeatable(ExpressionSyntax expression) {
        foreach (var node in expression.DescendantNodesAndSelf()) {
            switch (node) {
                case InvocationExpressionSyntax:
                case ObjectCreationExpressionSyntax:
                case ImplicitObjectCreationExpressionSyntax:
                case AwaitExpressionSyntax:
                case AssignmentExpressionSyntax:
                case ElementAccessExpressionSyntax:
                case ImplicitElementAccessSyntax:
                case AnonymousFunctionExpressionSyntax:
                case StackAllocArrayCreationExpressionSyntax:
                case ImplicitStackAllocArrayCreationExpressionSyntax:
                case ArrayCreationExpressionSyntax:
                case ImplicitArrayCreationExpressionSyntax:
                case RefExpressionSyntax:
                case MakeRefExpressionSyntax:
                case QueryExpressionSyntax:
                case WithExpressionSyntax:
                    return false;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                    || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                case PrefixUnaryExpressionSyntax prefix
                    when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    || prefix.IsKind(SyntaxKind.PreDecrementExpression)
                    || prefix.IsKind(SyntaxKind.AddressOfExpression):
                    return false;

                case ArgumentSyntax argument when !argument.RefKindKeyword.IsKind(SyntaxKind.None):
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether an expression is a path of locals, parameters, fields, <c>this</c> and <c>base</c> —
    ///     storage, with no accessor anywhere in it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Stricter than <see cref="IsRepeatable" /> by exactly one thing: no property or event
    ///     access. <c>SK2061</c> needs it because a property getter is a method and two reads of it
    ///     inside one expression are two calls, and because <c>SK2012</c> already reports the
    ///     automatic-property case with a proof about the accessors that this rule does not have.
    ///     Reporting both would be one defect counted twice.
    /// </remarks>
    public static bool IsStableDataPath(SemanticModel model, ExpressionSyntax expression, CancellationToken cancel) {
        expression = Strip(expression);
        while (true) {
            switch (expression) {
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                    return true;

                case IdentifierNameSyntax:
                    return IsStorage(model.GetSymbolInfo(expression, cancel).Symbol);

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    if (!IsStorage(model.GetSymbolInfo(access, cancel).Symbol)) {
                        return false;
                    }

                    expression = Strip(access.Expression);
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    ///     A namespace or a type is a qualifier rather than a read, so a chain may end in one.
    /// </summary>
    static bool IsStorage(ISymbol? symbol) =>
        symbol is ILocalSymbol
            or IParameterSymbol
            or IFieldSymbol
            or INamespaceSymbol
            or ITypeSymbol;

    static ExpressionSyntax Strip(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses.Expression;
        }

        return expression;
    }
}
