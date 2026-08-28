using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>f(number: 1)</c> ⇔ <c>f(1)</c>, under the four <c>resharper_arguments_*</c> keys.
/// </summary>
/// <remarks>
///     ⚠ Four keys and one rewrite, and they are genuinely four: the argument's *kind* selects which key
///     governs it. Measured against the oracle with one key flipped and the other three left alone —
///     with <c>resharper_arguments_literal = named</c> the literal <c>1</c> gains a name while the
///     string, the lambda and the <c>new()</c> beside it stay positional. Implementing them as one
///     boolean would have made three of the four unobservable.
///     <para>
///         ⚠ Like <see cref="NamespaceBodyRule" />, this needs a cleanup task the first sweep missed:
///         <c>ArrangeArgumentsStyle</c>. Without it the oracle leaves every named argument in place and the
///         four keys look like settings the reference tool ignores.
///     </para>
///     <para>
///         ⚠ `out`/`ref`/`in` arguments are treated like any other, and that was measured rather than
///         assumed. The first version exempted them on the reasoning that the name is often the only thing
///         distinguishing one <c>out</c> parameter from the next; the oracle strips them, and an exemption
///         nobody can point at a rule for is a divergence with no entry in docs/divergences.md.
///     </para>
///     <para>
///         ⚠ Adding a name is semantic — the parameter's name comes from the resolved overload — and so is
///         removing one, because a named argument may be *out of order*. <c>f(text: "x", number: 1)</c> must
///         not become <c>f("x", 1)</c>; the name is only redundant when the argument already sits at its own
///         parameter's position. That check is the whole reason this rule binds rather than pattern-matches.
///     </para>
/// </remarks>
public sealed class ArgumentStyleRule : ArrangementRule {
    public override string Id => ArrangeIds.ArgumentStyle;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    /// <summary>Which of the five keys governs an argument, by the shape of its expression.</summary>
    /// <remarks>
    ///     ⚠ <c>arguments_named</c> is the fifth and it is a <em>partition</em> of what used to fall to
    ///     <c>arguments_other</c>, not a refinement of it. Measured both ways round: with
    ///     <c>arguments_named = named</c> the oracle names an identifier and a member access and nothing
    ///     else, and with <c>arguments_other = named</c> instead it names the complement — an
    ///     invocation, a binary expression, a cast, an element access, <c>typeof</c>, <c>nameof</c>,
    ///     <c>default</c>, <c>new</c>, a conditional. Folding the two together would make one of them
    ///     unobservable, which is the same mistake the four-key note above records.
    ///     <para>
    ///         ⚠ A parenthesised name is a name. The oracle names <c>(local)</c> as a named expression, which
    ///         is not a special case here: <c>RemoveRedundantParentheses</c> runs first in the cleanup profile
    ///         and this rule sees the bare identifier. <see cref="ArrangementPipeline" /> orders Skala's
    ///         rules the same way.
    ///     </para>
    /// </remarks>
    internal static ArgumentStyle StyleFor(ExpressionSyntax expression, in ArrangementOptions options) =>
        expression switch {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
                or InterpolatedStringExpressionSyntax => options.ArgumentsStringLiteral,
            LiteralExpressionSyntax => options.ArgumentsLiteral,
            AnonymousFunctionExpressionSyntax => options.ArgumentsAnonymousFunction,
            SimpleNameSyntax or MemberAccessExpressionSyntax => options.ArgumentsNamed,
            _ => options.ArgumentsOther
        };

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node) {
            var visited = (ArgumentListSyntax)base.VisitArgumentList(node)!;

            // resharper_arguments_skip_single: a one-argument call is left exactly as written.
            // ⚠ Checked here and not per argument. The key gates the *call*, which is what makes it
            // observable at all: a per-argument reading would exempt the last argument of every call
            // and change every list of two or more, which is not what the oracle does.
            if (options.ArgumentsSkipSingle && visited.Arguments.Count == 1) {
                return visited;
            }

            // ⚠ `params`, `__arglist` and an unresolved call all make the positional mapping a guess.
            // The rule declines rather than guesses.
            if (model.GetSymbolInfo(node.Parent!).Symbol is not IMethodSymbol method
                || method.Parameters.Any(static p => p.IsParams)
                || method.Parameters.Length < node.Arguments.Count) {
                return visited;
            }

            var arguments0 = visited.Arguments;

            // ⚠ An out-of-position named argument may not be followed by an unnamed one (CS8323),
            // so removing a name is only legal *before* the first argument whose name is keeping the
            // call together. Without this the rule turned `Brush(radius: 1, falloff: f, Curve)` into
            // a file that does not compile — caught by safety layer 2 on Vixen's TerrainBrushTests,
            // which is layer 2 doing its job and not a reason to leave the rule wrong.
            var firstOutOfPosition = arguments0.Count;
            for (var i = 0; i < arguments0.Count; i++) {
                if (arguments0[i].NameColon is { } held
                    && (i >= method.Parameters.Length
                        || !string.Equals(
                            method.Parameters[i].Name,
                            held.Name.Identifier.ValueText,
                            StringComparison.Ordinal
                        ))) {
                    firstOutOfPosition = i;
                    break;
                }
            }

            var arguments = visited.Arguments;
            var changed = false;
            for (var i = 0; i < arguments.Count; i++) {
                var argument = arguments[i];
                if (i > firstOutOfPosition) {
                    continue;
                }

                var wanted = StyleFor(argument.Expression, options);
                if (argument.NameColon is null && wanted == ArgumentStyle.Named) {
                    if (i >= method.Parameters.Length) {
                        continue;
                    }

                    var name = method.Parameters[i].Name;
                    if (name.Length == 0 || !SyntaxFacts.IsValidIdentifier(name)) {
                        continue;
                    }

                    arguments = arguments.Replace(
                        argument,
                        argument.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(name)))
                            .WithLeadingTrivia(argument.GetLeadingTrivia())
                    );

                    changed = true;
                    continue;
                }

                if (argument.NameColon is not { } colon || wanted != ArgumentStyle.Positional) {
                    continue;
                }

                // ⚠ The name is only redundant where the argument is already in position. A named
                // argument used to *reorder* a call carries meaning, and dropping its name silently
                // swaps the operands.
                if (i >= method.Parameters.Length
                    || !string.Equals(
                        method.Parameters[i].Name,
                        colon.Name.Identifier.ValueText,
                        StringComparison.Ordinal
                    )) {
                    continue;
                }

                arguments = arguments.Replace(
                    argument,
                    argument.WithNameColon(null).WithLeadingTrivia(argument.GetLeadingTrivia())
                );

                changed = true;
            }

            return changed ? visited.WithArguments(arguments) : visited;
        }
    }
}

/// <summary>
///     <c>out _</c> ⇒ <c>out var _</c>, under <c>resharper_prefer_explicit_discard_declaration</c>.
/// </summary>
/// <remarks>
///     ⚠ The export writes <c>false</c>, at which value this rule does nothing at all on this
///     repository's configuration — the observable direction is the other one. It is implemented rather
///     than recorded as inert because the key *is* observable when set: measured, at <c>true</c> the
///     oracle turns <c>Deconstruct(out var p, out _)</c> into <c>… out var _)</c>.
///     <para>
///         ⚠ It deliberately does not do the reverse. At <c>false</c> the oracle does **not** strip an
///         existing <c>var</c> from a discard: <c>out int _</c> becomes <c>out var _</c> under the <c>var</c>
///         rule and stays there. `false` means "do not add", not "remove".
///     </para>
/// </remarks>
public sealed class DiscardDeclarationRule : ArrangementRule {
    public override string Id => ArrangeIds.DiscardDeclaration;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => options.PreferExplicitDiscardDeclaration;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Guard).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitArgument(ArgumentSyntax node) {
            var visited = (ArgumentSyntax)base.VisitArgument(node)!;

            // Only an `out _`; a bare `_` in any other position may well be a real variable.
            if (node.RefKindKeyword.IsKind(SyntaxKind.None)
                || node.Expression is not IdentifierNameSyntax { Identifier.ValueText: "_" }) {
                return visited;
            }

            return visited.WithExpression(
                SyntaxFactory.DeclarationExpression(
                    SyntaxFactory.IdentifierName("var"),
                    SyntaxFactory.DiscardDesignation()
                )
                    .WithTriviaFrom(visited.Expression)
            );
        }
    }
}
