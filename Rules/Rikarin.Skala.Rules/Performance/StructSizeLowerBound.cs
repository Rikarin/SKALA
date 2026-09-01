using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>A target-independent lower bound, not Marshal.SizeOf or an estimate of padding.</summary>
internal static class StructSizeLowerBound {
    public static long Read(ITypeSymbol type, SemanticModel model, CancellationToken cancellation) =>
        Read(
            type,
            model,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default),
            new Dictionary<ITypeSymbol, long>(SymbolEqualityComparer.Default),
            0,
            cancellation
        );

    static long Read(
        ITypeSymbol type,
        SemanticModel model,
        HashSet<ITypeSymbol> active,
        Dictionary<ITypeSymbol, long> memo,
        int depth,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();
        if (memo.TryGetValue(type, out var known)) {
            return known;
        }

        var primitive = type.SpecialType switch {
            SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
            SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
            SpecialType.System_Decimal => 16,
            _ => 0
        };
        if (primitive != 0) {
            return primitive;
        }

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } structure
            || depth >= 32
            || structure.DeclaringSyntaxReferences.IsEmpty
            || structure.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree != model.SyntaxTree
                || reference.GetSyntax(cancellation) is not TypeDeclarationSyntax declaration
                || declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
            || structure.GetAttributes()
                .Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString()
                    == "System.Runtime.InteropServices.StructLayoutAttribute"
                    && (attribute.ConstructorArguments.Length != 1
                        || attribute.ConstructorArguments[0].Value is not 0
                        || attribute.ApplicationSyntaxReference is not { } reference
                        || reference.SyntaxTree != model.SyntaxTree
                        || !ConstantDependencies.AreFileLocal(model, reference.GetSyntax(cancellation), cancellation))
                )
            || !active.Add(type)) {
            return 0;
        }

        long size = 0;
        foreach (var field in structure.GetMembers().OfType<IFieldSymbol>()) {
            if (!field.IsStatic && field.RefKind == RefKind.None && !field.IsFixedSizeBuffer) {
                size = System.Math.Min(
                    int.MaxValue,
                    size + Read(field.Type, model, active, memo, depth + 1, cancellation)
                );
            }
        }

        active.Remove(type);
        memo[type] = size;
        return size;
    }
}
