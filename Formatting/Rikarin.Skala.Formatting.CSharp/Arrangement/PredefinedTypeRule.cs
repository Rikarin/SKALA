using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>Int32</c> ⇒ <c>int</c>, <c>String.Empty</c> ⇒ <c>string.Empty</c>.
/// </summary>
/// <remarks>
/// ⚠ <c>dotnet_style_predefined_type_for_locals_parameters_members = true</c>, and
/// <c>resharper_builtin_type_apply_to_native_integer = false</c> — so <c>nint</c> stays <c>nint</c>
/// and is never spelled <c>IntPtr</c> or the other way round. That exception is the reason this is a
/// rule rather than a table lookup: <c>IntPtr</c> and <c>UIntPtr</c> have predefined spellings in
/// modern C# and the author has deliberately declined them.
/// </remarks>
public sealed class PredefinedTypeRule : ArrangementRule {
    public override string Id => ArrangeIds.PredefinedType;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) => options.PredefinedTypeForLocals;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Semantics).Visit(context.Root);

    sealed class Rewriter(SemanticModel model) : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            return Replace(node, visited);
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node) {
            var visited = (QualifiedNameSyntax)base.VisitQualifiedName(node)!;
            return Replace(node, visited);
        }

        SyntaxNode Replace(SyntaxNode original, SyntaxNode visited) {
            // ⚠ Only a type *reference* is rewritten. `using System;` names a namespace, a
            // `nameof(Int32)` reads the identifier, and neither is a place `int` may be written.
            if (original.Parent is UsingDirectiveSyntax or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax
                || original.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax invocation }
                && invocation.Expression.ToString().StartsWith("nameof", StringComparison.Ordinal)) {
                return visited;
            }

            if (original.Parent is QualifiedNameSyntax { Right: var right } && right == original) {
                // The whole qualified name is handled by VisitQualifiedName; its right-hand
                // identifier on its own is not a type reference.
                return visited;
            }

            if (model.GetSymbolInfo(original).Symbol is not ITypeSymbol type || Keyword(type) is not { } keyword) {
                return visited;
            }

            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(keyword))
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        /// <summary>
        /// The keyword spelling of a special type, or null when the type has none Skala will apply.
        /// </summary>
        /// <remarks>
        /// ⚠ <c>System_IntPtr</c> and <c>System_UIntPtr</c> are deliberately absent:
        /// <c>builtin_type_apply_to_native_integer = false</c>. <c>void</c> is absent because a
        /// <c>System.Void</c> reference is never something a person wrote.
        /// </remarks>
        static SyntaxKind? Keyword(ITypeSymbol type) =>
            type.SpecialType switch {
                SpecialType.System_Boolean => SyntaxKind.BoolKeyword,
                SpecialType.System_Byte => SyntaxKind.ByteKeyword,
                SpecialType.System_SByte => SyntaxKind.SByteKeyword,
                SpecialType.System_Int16 => SyntaxKind.ShortKeyword,
                SpecialType.System_UInt16 => SyntaxKind.UShortKeyword,
                SpecialType.System_Int32 => SyntaxKind.IntKeyword,
                SpecialType.System_UInt32 => SyntaxKind.UIntKeyword,
                SpecialType.System_Int64 => SyntaxKind.LongKeyword,
                SpecialType.System_UInt64 => SyntaxKind.ULongKeyword,
                SpecialType.System_Single => SyntaxKind.FloatKeyword,
                SpecialType.System_Double => SyntaxKind.DoubleKeyword,
                SpecialType.System_Decimal => SyntaxKind.DecimalKeyword,
                SpecialType.System_Char => SyntaxKind.CharKeyword,
                SpecialType.System_String => SyntaxKind.StringKeyword,
                SpecialType.System_Object => SyntaxKind.ObjectKeyword,
                _ => null
            };
    }
}
