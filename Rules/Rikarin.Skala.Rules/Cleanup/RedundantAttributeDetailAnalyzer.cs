using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0261</c> — an attribute spelling out what the language supplies for free.</summary>
/// <remarks>
///     <para>
///         Two shapes: the <c>Attribute</c> suffix the attribute-name lookup appends on its own, and an
///         <c>[AttributeUsage]</c> argument that restates that attribute's own default.
///     </para>
///     <para>
///         ⚠ <b>Dropping the suffix is a lookup, not a string operation.</b> <c>[FooAttribute]</c>
///         resolves to <c>FooAttribute</c>; <c>[Foo]</c> searches for <em>both</em> <c>Foo</c> and
///         <c>FooAttribute</c>. So any type named <c>Foo</c> in scope changes what the shortened form
///         means — or makes it <c>CS1614</c> when both are attribute classes. The finding is withdrawn
///         if <see cref="SemanticModel.LookupNamespacesAndTypes" /> returns anything at all for the
///         short name.
///     </para>
///     <para>
///         ⚠ <b>That is deliberately stricter than the language.</b> A non-attribute <c>Foo</c> beside
///         an attribute <c>FooAttribute</c> is in fact still unambiguous, and is declined anyway:
///         "is the other candidate an attribute class" is a second question, and getting it wrong
///         changes which type is applied to the declaration.
///     </para>
///     <para>
///         ⚠ <b>Only a simple name is reported.</b> A qualified <c>[System.SerializableAttribute]</c>
///         carries a second redundancy in the qualifier, and that span and concept belong to
///         <c>SK0243</c>. Leaving the qualified form to it keeps one node out of two rules' fixes in a
///         single pass.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The <c>[AttributeUsage]</c> half asks the attribute's identity rather than its
///             name.
///         </b> Somebody else's <c>AttributeUsageAttribute</c> in another namespace has whatever
///         defaults it declares, and they are not <c>Inherited = true</c> and
///         <c>AllowMultiple = false</c> because it says so.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantAttributeDetailAnalyzer : DiagnosticAnalyzer {
    const string Suffix = "Attribute";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantAttributeDetail);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var attribute = (AttributeSyntax)context.Node;
        AnalyzeSuffix(context, attribute);
        AnalyzeUsageArguments(context, attribute);
    }

    /// <summary><c>[SerializableAttribute]</c> → <c>[Serializable]</c>.</summary>
    static void AnalyzeSuffix(SyntaxNodeAnalysisContext context, AttributeSyntax attribute) {
        if (attribute.Name is not IdentifierNameSyntax name) {
            return;
        }

        var written = name.Identifier.ValueText;
        if (written.Length <= Suffix.Length || !written.EndsWith(Suffix, System.StringComparison.Ordinal)) {
            return;
        }

        var shortened = written.Substring(0, written.Length - Suffix.Length);

        // ⚠ `class voidAttribute` is legal and `[void]` is a parse error. The fix has to still be C#.
        if (SyntaxFacts.GetKeywordKind(shortened) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(shortened) == SyntaxKind.VarKeyword) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute.Name, context.CancellationToken).Symbol is null) {
            return;
        }

        // ⚠ Anything at all answering to the short name withdraws the finding; see the type remarks.
        if (!context.SemanticModel
                .LookupNamespacesAndTypes(name.SpanStart, null, shortened)
                .IsDefaultOrEmpty) {
            return;
        }

        // ⚠ There is deliberately no comment-or-directive guard on this shape, and the one written
        // here first was dead. The deleted span is the tail of a single identifier token, and an
        // identifier cannot contain trivia — no `//`, no `/*` and no `#` can ever appear inside
        // `name.Span`. Sabotaging the guard turned nothing red because no fixture could reach it,
        // which is the signal that the guard was not protecting anything. The `[AttributeUsage]`
        // shape below spans several tokens and does need one.
        var span = TextSpan.FromBounds(name.Span.End - Suffix.Length, name.Span.End);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                name.GetLocation(),
                FixEdits.Pack((span, string.Empty)),
                $"the attribute-name lookup appends `{Suffix}`, so `{shortened}` names the same attribute"
            )
        );
    }

    /// <summary><c>Inherited = true</c> and <c>AllowMultiple = false</c> on <c>[AttributeUsage]</c>.</summary>
    /// <remarks>
    ///     ⚠ The deleted span starts at the <em>end of the preceding argument</em> so the separating
    ///     comma goes with it. <c>AttributeUsage</c>'s <c>validOn</c> is a required positional argument,
    ///     so a named one is never first — the index guard is there because relying on that would be
    ///     relying on the caller's code being valid, which an analyzer running under
    ///     <c>--load=loose</c> may not.
    /// </remarks>
    static void AnalyzeUsageArguments(SyntaxNodeAnalysisContext context, AttributeSyntax attribute) {
        if (attribute.ArgumentList is not { Arguments.Count: > 1 } list
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol { ContainingType: { Name: "AttributeUsageAttribute" } usage }
            || usage.ContainingNamespace is not { Name: "System", ContainingNamespace.IsGlobalNamespace: true }) {
            return;
        }

        for (var index = 1; index < list.Arguments.Count; index++) {
            var argument = list.Arguments[index];
            var written = argument.NameEquals?.Name.Identifier.ValueText;
            var restated = written switch {
                "Inherited" => argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression),
                "AllowMultiple" => argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression),
                _ => false
            };

            if (!restated) {
                continue;
            }

            var span = TextSpan.FromBounds(list.Arguments[index - 1].Span.End, argument.Span.End);
            if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(argument.SyntaxTree, span)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    argument.GetLocation(),
                    FixEdits.Pack((span, string.Empty)),
                    $"`{written}` already has this value on every attribute that does not say otherwise"
                )
            );
        }
    }
}
