using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

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
///         </b> <c>M(out var _)</c> becomes <c>M(out _)</c> under
///         <c>skala_prefer_explicit_discard_declaration</c>, which is a tier-A option
///         Skala performs through <c>SK0217</c>'s <c>DiscardDeclarationRule</c> — in both directions,
///         against the oracle. Reporting it here as well would be the double-count doc 17 § "Inspection
///         ids are not concepts" warns about, so a <c>VarPatternSyntax</c> is never matched. It could
///         not be matched anyway: <c>o is var _</c> does not become <c>o is var</c>, and a bare
///         <c>_</c> directly under an <c>is</c> is <c>CS0246</c> — the parser reads it as a type.
///     </para>
///     <para>
///         ⚠ <b>Purely syntactic, and deliberately.</b> No branch reads the semantic model, so the rule
///         runs — and is therefore measurable — under <c>--load=loose</c>. That is possible only because
///         a <em>designation</em> position cannot refer to something already in scope: it declares, and
///         <c>_</c> declares nothing. The scope lookup the <c>var _</c> reading would have needed is the
///         reason that reading was refused, and it is not needed for this one.
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
        context.RegisterCompilationStartAction(static start => {
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

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, discard.Span),
                FixEdits.Pack((span, string.Empty)),
                "The pattern's `_` designation declares nothing, so the pattern reads the same without it"
            )
        );
    }
}
