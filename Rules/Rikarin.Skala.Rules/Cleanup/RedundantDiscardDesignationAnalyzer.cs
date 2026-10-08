using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0250</c> — a pattern that names its match <c>_</c>, which is to say does not name it.
/// </summary>
/// <remarks>
///     <para>
///         <c>o is string _</c> is <c>o is string</c>, and <c>o is Point { X: 1 } _</c> is
///         <c>o is Point { X: 1 }</c>. The designation declares a discard: it introduces no name, binds
///         nothing and changes no match. It is the last thing a reader of the pattern arrives at and it
///         says that the previous few words were all of it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The <c>var _</c> spelling is a different question and belongs to a different rule
///             that already ships.
///         </b>
///         <c>M(out var _)</c> becomes <c>M(out _)</c> under
///         <c>skala_prefer_explicit_discard_declaration</c>, which is a tier-A option
///         Skala performs through <c>SK0217</c>'s <c>DiscardDeclarationRule</c> — in both directions,
///         against the oracle. Reporting it here as well would be the double-count doc 17 § "Inspection
///         ids are not concepts" warns about, so a <c>VarPatternSyntax</c> is never matched. It could
///         not be matched anyway: <c>o is var _</c> does not become <c>o is var</c>, and a bare
///         <c>_</c> directly under an <c>is</c> is <c>CS0246</c> — the parser reads it as a type.
///     </para>
///     <para>
///         ⚠ <b>Purely syntactic once, and it was half the question</b> (#424). A <em>designation</em>
///         position cannot refer to something already in scope — it declares, and <c>_</c> declares
///         nothing — so deciding the finding needs no lookup. What the pattern means without it does:
///         <c>case Random _:</c> without the designation is bound as an expression first, and a
///         constant called <c>Random</c> takes it. See <see cref="MeansTheSame" />; the rule is
///         <c>Semantic</c> for that and no longer runs under <c>--load=loose</c>.
///     </para>
///     <para>
///         ⚠ <b>The language floor is 9.0 and it is not decoration.</b> <c>o is string</c> has been legal
///         since C# 1 because it is the <c>is</c> <em>operator</em>, but <c>case string:</c> and
///         <c>string =&gt; …</c> are bare <em>type patterns</em>, which are C# 9 — measured, at
///         <c>CS8400: Feature 'type pattern' is not available in C# 8.0</c>. Below the floor the rule is
///         silent rather than clever about which position it is in.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantDiscardDesignationAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.RedundantDiscardDesignation);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantDiscardDesignation);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.DeclarationPattern, SyntaxKind.RecursivePattern);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var designation = context.Node switch {
            DeclarationPatternSyntax pattern => pattern.Designation,
            RecursivePatternSyntax pattern => pattern.Designation,
            _ => null
        };

        if (designation is not DiscardDesignationSyntax discard || discard.UnderscoreToken.IsMissing) {
            return;
        }

        // ⚠ The whitespace before the `_` goes with it. Deleting the token alone leaves
        // `o is string ` with a trailing space inside the pattern, and while `skala fix` re-formats
        // every file it touches, a fix whose output the formatter has to repair is one whose edit was
        // wrong. The span therefore starts at the end of whatever token preceded the designation —
        // a type name, a closing brace or a closing parenthesis, depending on the shape.
        var span = TextSpan.FromBounds(discard.UnderscoreToken.GetPreviousToken().Span.End, discard.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(context.Node.SyntaxTree, span)) {
            return;
        }

        if (!MeansTheSame(context, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, discard.Span),
                FixEdits.Pack((span, string.Empty)),
                "The pattern's `_` designation declares nothing, so the pattern reads the same without it"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether the pattern without its <c>_</c> is still the pattern it was (#424).
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>"The designation declares nothing" was true and was not enough</b> (#412's audit, both
    ///     measured by running the two versions). Without it the parser and the binder read what is left
    ///     afresh:
    ///     <list type="bullet">
    ///         <item>
    ///             <c>o is (1) _</c> is a positional pattern — a <c>Deconstruct</c> or <c>ITuple</c> of
    ///             one element — and <c>o is (1)</c> is the constant <c>1</c> in parentheses. A recursive
    ///             pattern must therefore re-parse as itself without the designation.
    ///         </item>
    ///         <item>
    ///             <c>case Random _:</c> declares a <em>type</em>, but <c>case Random:</c> and
    ///             <c>Random =&gt;</c> are bound as an <em>expression</em> first and as a type only if
    ///             that fails, so a constant called <c>Random</c> in scope takes the label. Only directly
    ///             under <c>is</c> does the type win, because <c>o is Random</c> is the <c>is</c>
    ///             operator.
    ///         </item>
    ///     </list>
    ///     ⚠ The second is a lookup, so this rule now needs the semantic model and no longer runs under
    ///     <c>--load=loose</c>: a <c>Syntax</c> scope would have been a claim that one file's text
    ///     answers it, and the constant can be declared anywhere.
    /// </remarks>
    static bool MeansTheSame(SyntaxNodeAnalysisContext context, TextSpan span) {
        var pattern = (PatternSyntax)context.Node;
        var reparsed = FixReparse.Reparsed(
            pattern.Parent ?? pattern,
            [(span, string.Empty)],
            context.CancellationToken
        );
        if (reparsed is null) {
            return false;
        }

        if (pattern is RecursivePatternSyntax recursive) {
            return reparsed.DescendantNodesAndSelf()
                .OfType<RecursivePatternSyntax>()
                .Any(candidate => candidate.SpanStart == recursive.SpanStart
                    && FixReparse.Equivalent(candidate, recursive.WithDesignation(null))
                );
        }

        var declared = (DeclarationPatternSyntax)pattern;
        var type = reparsed.DescendantNodesAndSelf()
            .Where(node => node.Span == declared.Type.Span)
            .OrderBy(static node => node.Ancestors().Count())
            .FirstOrDefault();
        if (type is null) {
            return false;
        }

        // `o is Random` is the `is` operator, which binds its right-hand side as a type, and only a name
        // can mean something else: `int[]`, `int?` or a tuple type is a type wherever it is written.
        if (type.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression }
            || declared.Type is not NameSyntax) {
            return true;
        }

        var intended = context.SemanticModel.GetTypeInfo(declared.Type, context.CancellationToken).Type;
        return intended is not null
            && FixRebind.Same(
                FixRebind.AsExpression(context.SemanticModel, declared.Type.SpanStart, declared.Type),
                intended
            );
    }
}
