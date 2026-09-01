using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>Do not let a per-file diagnostic depend on another source file's constant initializer.</summary>
internal static class ConstantDependencies {
    public static bool AreFileLocal(SemanticModel model, SyntaxNode expression, CancellationToken cancellation) =>
        Check(model, expression, new HashSet<ISymbol>(SymbolEqualityComparer.Default), cancellation);

    static bool Check(
        SemanticModel model,
        SyntaxNode expression,
        HashSet<ISymbol> visited,
        CancellationToken cancellation
    ) {
        foreach (var node in expression.DescendantNodesAndSelf()) {
            if (node is not IdentifierNameSyntax
                || model.GetSymbolInfo(node, cancellation).Symbol is not { } symbol
                || symbol is not (IFieldSymbol { IsConst: true } or ILocalSymbol { IsConst: true })
                || !visited.Add(symbol)) {
                continue;
            }

            foreach (var reference in symbol.DeclaringSyntaxReferences) {
                if (reference.SyntaxTree != expression.SyntaxTree) {
                    return false;
                }

                var declaration = reference.GetSyntax(cancellation);
                // Implicit enum values depend on preceding members too.
                if (declaration is EnumMemberDeclarationSyntax && declaration.Parent is { } enumeration) {
                    declaration = enumeration;
                }

                if (!Check(model, declaration, visited, cancellation)) {
                    return false;
                }
            }
        }

        return true;
    }
}
