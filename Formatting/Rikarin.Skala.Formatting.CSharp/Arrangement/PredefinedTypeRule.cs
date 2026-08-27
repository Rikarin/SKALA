using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>Int32</c> ⇒ <c>int</c>, <c>String.Empty</c> ⇒ <c>string.Empty</c>.
/// </summary>
/// <remarks>
///     ⚠ <c>dotnet_style_predefined_type_for_locals_parameters_members = true</c>, and
///     <c>resharper_builtin_type_apply_to_native_integer = false</c> — so <c>nint</c> stays <c>nint</c>
///     and is never spelled <c>IntPtr</c> or the other way round. That exception is the reason this is a
///     rule rather than a table lookup: <c>IntPtr</c> and <c>UIntPtr</c> have predefined spellings in
///     modern C# and the author has deliberately declined them.
/// </remarks>
public sealed class PredefinedTypeRule : ArrangementRule {
    public override string Id => ArrangeIds.PredefinedType;

    public override bool NeedsSemantics => true;

    /// <summary>
    ///     ⚠ Enabled when *either* key asks for it, because the two govern different positions and the
    ///     rewriter checks them one node at a time.
    /// </summary>
    public override bool IsEnabled(in ArrangementOptions options) =>
        options.PredefinedTypeForLocals || options.PredefinedTypeForMemberAccess;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            return Replace(node, visited);
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node) {
            var visited = (QualifiedNameSyntax)base.VisitQualifiedName(node)!;
            return Replace(node, visited);
        }

        SyntaxNode Replace(SyntaxNode original, SyntaxNode visited) {
            // ⚠ `var` is an IdentifierNameSyntax, and asking the model about it returns the
            // *inferred* type — so without this line the rule rewrites `out var value` into
            // `out string value` and un-`var`s the entire repository, which is the exact opposite of
            // what `csharp_style_var_* = true` asks for. It fired on 2 210 of Vixen's 4 606 files
            // before this was found, and it was found by chasing 567 re-bind reverts (`out var
            // value` whose flow state is maybe-null becomes `out string value`, which is CS8600)
            // rather than by reading the rule. `dynamic` is skipped for the same reason: it is a
            // contextual keyword parsed as an identifier, and it has no predefined spelling.
            if (original is IdentifierNameSyntax { Identifier.ValueText: "var" or "dynamic" }) {
                return visited;
            }

            // ⚠ Only a type *reference* is rewritten. `using System;` names a namespace and
            // `nameof(Int32)` reads an identifier whose spelling is the value — neither is a place
            // `int` may be written.
            if (original.Parent is UsingDirectiveSyntax
                or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax
                || IsInsideNameOf(original)) {
                return visited;
            }

            if (original.Parent is QualifiedNameSyntax { Right: var right } && right == original) {
                // The whole qualified name is handled by VisitQualifiedName; its right-hand
                // identifier on its own is not a type reference.
                return visited;
            }

            // ⚠ Two keys, two positions. `Int32.MaxValue` is a member access and is governed by
            // `dotnet_style_predefined_type_for_member_access`; `Int32 x` is a declaration and is
            // governed by `dotnet_style_predefined_type_for_locals_parameters_members`. Reading only
            // the second and applying it to both is what this rule did before, and it is why
            // docs/plan/17 found the member-access key at Tier D while the behaviour it names was
            // already shipping — implemented, but credited to the wrong option and unobservable
            // through its own.
            var isReceiverOfMemberAccess =
                original.Parent is MemberAccessExpressionSyntax access && access.Expression == original;

            if (!(isReceiverOfMemberAccess ? options.PredefinedTypeForMemberAccess : options.PredefinedTypeForLocals)) {
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
        ///     Whether the node is an argument of a <c>nameof</c>.
        /// </summary>
        /// <remarks>
        ///     ⚠ Found by safety layer 2 rather than by review, which is the point of having one. The
        ///     first version of this guard looked for a <c>MemberAccessExpressionSyntax</c> parent and
        ///     so never matched <c>nameof(Int32)</c> at all — the identifier there is an
        ///     <c>ArgumentSyntax</c>, two nodes below the invocation. The rewrite produced
        ///     <c>nameof(int)</c>, the re-bind reported <c>CS1525: Invalid expression term 'int'</c>,
        ///     and the file was reverted instead of corrupted.
        /// </remarks>
        static bool IsInsideNameOf(SyntaxNode node) {
            for (var current = node; current is not null; current = current.Parent) {
                if (current.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation }
                    && invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }) {
                    return true;
                }

                if (current is StatementSyntax or MemberDeclarationSyntax) {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        ///     The keyword spelling of a special type, or null when the type has none Skala will apply.
        /// </summary>
        /// <remarks>
        ///     ⚠ <c>System_IntPtr</c> and <c>System_UIntPtr</c> are deliberately absent:
        ///     <c>builtin_type_apply_to_native_integer = false</c>. <c>void</c> is absent because a
        ///     <c>System.Void</c> reference is never something a person wrote.
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
