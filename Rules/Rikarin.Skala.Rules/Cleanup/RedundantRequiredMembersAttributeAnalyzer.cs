using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0281</c> — <c>[SetsRequiredMembers]</c> on a constructor with nothing to set.</summary>
/// <remarks>
///     <para>
///         The attribute is a promise to the compiler: this constructor assigns every <c>required</c>
///         member, so a caller need not use an object initializer. On a type that declares no
///         <c>required</c> member and inherits none, the promise is about an empty set — and it is
///         worse than noise, because it tells a reader that requiredness is in play here when it is not,
///         and it keeps working silently if somebody later adds a <c>required</c> member the
///         constructor does not assign.
///     </para>
///     <para>
///         ⚠ <b>The base chain is part of the question, not a refinement of it.</b>
///         <c>[SetsRequiredMembers]</c> covers the required members of the whole hierarchy, so a type
///         with none of its own but a base that has one is doing exactly what the attribute is for. The
///         walk goes all the way to <c>object</c>; stopping at the declared type would have been the
///         rule's worst false positive and it has a fixture.
///     </para>
///     <para>
///         ⚠ <b>An error type anywhere in the base chain withdraws the finding.</b> Under a loose load
///         a base class in another assembly binds to an error symbol whose members are empty — which
///         reads exactly like a base with no required members, and would turn every inheriting
///         constructor into a false positive. The rule is <c>requiresSemantics</c>, so it does not run
///         at all without a compilation, but a compilation with missing references is the ordinary case
///         rather than the exotic one.
///     </para>
///     <para>
///         ⚠ <b>The attribute's identity is asked, never its name.</b> Somebody's own
///         <c>SetsRequiredMembersAttribute</c> in another namespace means whatever they made it mean.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantRequiredMembersAttributeAnalyzer : DiagnosticAnalyzer {
    const string AttributeName = "SetsRequiredMembersAttribute";

    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.RedundantRequiredMembersAttribute);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.Parent is not AttributeListSyntax { Parent: ConstructorDeclarationSyntax constructor } list) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol { ContainingType: { Name: AttributeName } marker }
            || !IsCodeAnalysisNamespace(marker.ContainingNamespace)) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(constructor, context.CancellationToken)
            is not { ContainingType: { } type }) {
            return;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            if (current.TypeKind == TypeKind.Error) {
                return;
            }

            foreach (var member in current.GetMembers()) {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }) {
                    return;
                }
            }
        }

        var span = DeletedSpan(list, attribute);
        if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                attribute.GetLocation(),
                FixEdits.Pack((span, string.Empty)),
                $"`{type.Name}` declares and inherits no `required` member, so the attribute promises "
                + "to set an empty set"
            )
        );
    }

    static bool IsCodeAnalysisNamespace(INamespaceSymbol? symbol) =>
        symbol is {
            Name: "CodeAnalysis",
            ContainingNamespace:
            {
                Name: "Diagnostics",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            }
        };

    /// <summary>
    ///     ⚠ The whole list when this is its only attribute, the attribute and a comma when it is not.
    /// </summary>
    /// <remarks>
    ///     Deleting just the attribute out of <c>[A, SetsRequiredMembers]</c> leaves <c>[A, ]</c>, and
    ///     out of a sole <c>[SetsRequiredMembers]</c> leaves <c>[]</c> — neither parses. The sole case
    ///     takes the list's <em>full</em> span up to the next token so the line goes with it.
    /// </remarks>
    static TextSpan DeletedSpan(AttributeListSyntax list, AttributeSyntax attribute) {
        if (list.Attributes.Count == 1) {
            return TextSpan.FromBounds(list.SpanStart, list.GetLastToken().GetNextToken().SpanStart);
        }

        var index = list.Attributes.IndexOf(attribute);
        return index > 0
            ? TextSpan.FromBounds(list.Attributes[index - 1].Span.End, attribute.Span.End)
            : TextSpan.FromBounds(attribute.SpanStart, list.Attributes[1].SpanStart);
    }
}
