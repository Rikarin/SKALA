using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>A closed, file-local reference set for private-field rewrites.</summary>
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
            || RewriteGuards.ContainsCommentOrDirective(declaration)
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
