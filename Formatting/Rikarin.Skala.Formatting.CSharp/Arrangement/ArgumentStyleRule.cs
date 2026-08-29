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

            // ⚠ An unresolved call and `__arglist` make the positional mapping a guess, and the rule
            // declines rather than guesses. A `params` call does not, and used to be lumped in with
            // them — the second condition read `method.Parameters.Any(p => p.IsParams)`.
            // ⚠ MEASURED, unbatched, under the cleanup profile, which is what says the blanket
            // refusal was wrong in one direction and right in the other:
            //   written                        positional (the export)   named
            //   Many(first: 1, rest: [2, 3])   Many(1, [2, 3])           Many(first: 1, rest: [2, 3])
            //   Single(first: 1, 2, 3)         Single(1, 2, 3)           Single(first: 1, 2, 3)
            // The oracle strips a name off a `params` call like any other, and it adds one to the
            // `params` parameter itself only where the call is in *normal* form — `Single`'s expanded
            // arguments stay bare, because C# has no way to name them (CS1744). The refusal cost
            // `constructs/arrangement/lists/argument-style.cs` its baseline, and with it all four
            // `resharper_arguments_*` rows.
            if (model.GetSymbolInfo(node.Parent!).Symbol is not IMethodSymbol method) {
                return visited;
            }

            var parameters = method.Parameters;
            var paramsIndex = parameters.Length;
            for (var i = 0; i < parameters.Length; i++) {
                if (parameters[i].IsParams) {
                    paramsIndex = i;
                    break;
                }
            }

            // ⚠ More arguments than parameters is `__arglist` when there is no `params` parameter,
            // and an expanded `params` call when there is. Only the first is unmappable.
            if (paramsIndex == parameters.Length && parameters.Length < node.Arguments.Count) {
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
                    if (i >= parameters.Length) {
                        continue;
                    }

                    // ⚠ The `params` parameter may be named only where the call passes the array
                    // itself. In expanded form the arguments beyond `paramsIndex` have no parameter of
                    // their own to be named after — CS1744 — and the one *at* it is an element rather
                    // than the array, so naming it would not compile either. Recognised by the
                    // argument's converted type: normal form converts to the parameter's array type,
                    // expanded form to its element type. It is the direction this rule has been wrong
                    // in before, and the check is a compile-legality check rather than a style one.
                    if (i >= paramsIndex && !PassesTheParamsArray(node.Arguments, i, parameters[i])) {
                        continue;
                    }

                    var name = parameters[i].Name;
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

        /// <summary>
        ///     Whether the argument at <paramref name="index" /> is the <c>params</c> array itself rather
        ///     than one of its elements.
        /// </summary>
        /// <remarks>
        ///     ⚠ Read off the *original* argument list, because the visited one has been rebuilt and is
        ///     not in the tree the semantic model was created for — the same rule every semantic rewriter
        ///     in this assembly obeys.
        /// </remarks>
        bool PassesTheParamsArray(SeparatedSyntaxList<ArgumentSyntax> original, int index, IParameterSymbol parameter) {
            // ⚠ Only the last argument can be the array. Anything before it is an element by
            // construction, whatever its type.
            if (index != original.Count - 1) {
                return false;
            }

            var converted = model.GetTypeInfo(original[index].Expression).ConvertedType;
            return converted is not null
                && SymbolEqualityComparer.Default.Equals(converted, parameter.Type);
        }
    }
}

/// <summary>
///     <c>out _</c> ⇔ <c>out var _</c>, under <c>resharper_prefer_explicit_discard_declaration</c>.
/// </summary>
/// <remarks>
///     ⚠ <b>Both directions, and the export's <c>false</c> is not the inert one.</b> Asked directly,
///     unbatched, under the cleanup profile at both values:
///     <code>
/// written        false (the export)   true
/// out _          out _                out var _
/// out var _      out _                out var _
/// out int _      out var _            out var _
///     </code>
///     ⚠ The third row is where this entry was wrong until 06caa62f. It was read as "at <c>false</c>
///     the oracle does not strip an existing <c>var</c>", and the rule was written to do nothing at
///     <c>false</c> on the strength of it — but <c>out int _</c> is a *typed* declaration, so this key
///     declines it and the <c>var</c> rule turns it into <c>out var _</c> afterwards. It is a fact
///     about the shape that was probed and not about the key, and the row that answers the key is the
///     second one: at <c>false</c> an existing <c>var _</c> **is** stripped. The cost of the mistake
///     was `constructs/arrangement/lists/discard-declaration.cs` disagreeing with the oracle at the
///     sweep's baseline, which made this key's row attribute nothing.
///     <para>
///         ⚠ And the oracle is not at a fixed point on the third row: run over its own output it
///         answers <c>out _</c>, because the <c>var</c> it wrote in pass one is what this key removes in
///         pass two. Skala loops to a fixed point and therefore writes <c>out _</c> for all three, which
///         is the known asymmetry `sweep fixed-point` exists to report rather than a fourth behaviour.
///     </para>
/// </remarks>
public sealed class DiscardDeclarationRule : ArrangementRule {
    public override string Id => ArrangeIds.DiscardDeclaration;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Options.PreferExplicitDiscardDeclaration).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, bool explicitly) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitArgument(ArgumentSyntax node) {
            var visited = (ArgumentSyntax)base.VisitArgument(node)!;

            // Only an `out`/`ref`/`in` argument; a bare `_` in any other position may well be a real
            // variable, and a declaration is only legal here.
            if (node.RefKindKeyword.IsKind(SyntaxKind.None)) {
                return visited;
            }

            if (explicitly) {
                return node.Expression is IdentifierNameSyntax { Identifier.ValueText: "_" }
                    ? visited.WithExpression(
                        SyntaxFactory.DeclarationExpression(
                            SyntaxFactory.IdentifierName("var"),
                            SyntaxFactory.DiscardDesignation()
                        )
                            .WithTriviaFrom(visited.Expression)
                    )
                    : visited;
            }

            // ⚠ `var _` only. A discard declared with a written type — `out int _` — is left alone
            // here; the `var` rule is what decides whether that type stays, and the table above is
            // what says the two are separate decisions.
            return node.Expression is DeclarationExpressionSyntax {
                Type: IdentifierNameSyntax { Identifier.ValueText: "var" },
                Designation: DiscardDesignationSyntax
            }
                    ? visited.WithExpression(SyntaxFactory.IdentifierName("_").WithTriviaFrom(visited.Expression))
                    : visited;
        }
    }
}
