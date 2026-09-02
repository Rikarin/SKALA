using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>A closed, file-local reference set for private-field rewrites.</summary>
/// <remarks>
///     ⚠ <b>This helper deliberately does not ask the comment-or-directive question</b>, and it used
///     to (#325). It asked <c>ContainsCommentOrDirective(declaration)</c> over the field's FULL span
///     on behalf of all three callers — but the three rewrite different text, so one question could
///     not be right for all of them:
///     <list type="bullet">
///         <item>
///             <c>SK1003</c> deletes the whole field, so a doc comment above it is orphaned onto the
///             next member and the finding must withdraw — the node question.
///         </item>
///         <item>
///             <c>SK1022</c> and <c>SK1025</c> rewrite only the declared type and the initializer,
///             both strictly inside the declaration's own span, so a doc comment above the field is
///             text no edit touches — the span question.
///         </item>
///     </list>
///     ⚠ <b>The consequence was #302's exact defect in two rules #302 believed it had fixed.</b> Its
///     table named <c>SearchValuesAnalyzer</c> and moved that analyzer's visible guard onto the span
///     overload; the guard that actually silenced the rule was one call deeper, here, and a
///     documented <c>private static readonly</c> field — which is most of them — went on declining.
///     The question now lives at each call site, where the fix it protects is visible.
/// </remarks>
internal static class PrivateFieldUsage {
    public static bool TryRead(
        SemanticModel model,
        FieldDeclarationSyntax declaration,
        CancellationToken cancellation,
        System.Func<IFieldSymbol, bool> eligible,
        out IFieldSymbol field,
        out List<ExpressionSyntax> uses
    ) {
        field = null!;
        uses = new();
        if (declaration.Parent is not ClassDeclarationSyntax
            || declaration.Declaration.Variables.Count != 1
            || declaration.AttributeLists.Count != 0
            || declaration.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static type => type.Modifiers.Any(SyntaxKind.PartialKeyword))
            || model.GetDeclaredSymbol(declaration.Declaration.Variables[0], cancellation) is not IFieldSymbol {
                DeclaredAccessibility: Accessibility.Private
            } candidate
            || !eligible(candidate)) {
            return false;
        }

        var root = declaration.SyntaxTree.GetRoot(cancellation);
        if (root.ContainsDirectives) {
            return false;
        }

        foreach (var name in root.DescendantNodes(descendIntoTrivia: true).OfType<IdentifierNameSyntax>()) {
            cancellation.ThrowIfCancellationRequested();
            if (name.Identifier.ValueText != candidate.Name
                || model.GetSymbolInfo(name, cancellation).Symbol is not IFieldSymbol reference
                || !SymbolEqualityComparer.Default.Equals(reference.OriginalDefinition, candidate.OriginalDefinition)) {
                continue;
            }

            ExpressionSyntax expression = name;
            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name) {
                expression = access;
            }

            uses.Add(expression);
        }

        field = candidate;
        return uses.Count > 0;
    }

    public static INamedTypeSymbol? FrameworkType(Compilation compilation, string name) {
        var type = compilation.GetTypeByMetadataName(name);
        return type is not null && !type.Locations.Any(static location => location.IsInSource) ? type : null;
    }
}
