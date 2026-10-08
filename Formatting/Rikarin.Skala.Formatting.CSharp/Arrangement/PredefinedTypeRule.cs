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
    ///     ⚠ Always enabled: each key runs in both directions, so there is no value of the pair that asks
    ///     for nothing. <c>true</c> contracts <c>Int32</c> to <c>int</c>; <c>false</c> expands <c>int</c>
    ///     to <c>Int32</c> (#462). Enabled only when either key was <c>true</c>, as it used to be, the
    ///     rule could not run at all at the one value that asks for the expansion.
    /// </summary>
    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        /// <summary>
        ///     <c>int</c> ⇒ <c>Int32</c>, at the <c>false</c> value of whichever key owns the position.
        /// </summary>
        /// <remarks>
        ///     ⚠ Measured against <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c>, one key at a time
        ///     (#462, SK-DIV-0084). At <c>predefined_type_for_locals_parameters_members = false</c> the
        ///     oracle expands every predefined keyword but <c>void</c> in a field, property, indexer,
        ///     return, parameter (<c>ref</c>, <c>out</c>, <c>params</c> included), delegate, event and
        ///     operator signature, type argument, constraint, array, nullable, tuple element, cast,
        ///     <c>checked</c> cast, <c>typeof</c>, <c>sizeof</c>, <c>default(…)</c>, <c>is</c>,
        ///     <c>as</c>, type pattern, <c>stackalloc</c>, lambda parameter, local function and
        ///     <c>const</c> — and leaves a member access receiver to the sibling key, which at
        ///     <c>false</c> expands <c>int.MaxValue</c>, <c>int.TryParse</c> and <c>string.Empty</c> the
        ///     same way. ⚠ An enum's underlying type stays a keyword at <c>false</c>: the probe's
        ///     <c>byte</c> base came back untouched.
        ///     <para>
        ///         ⚠ The name is looked up at the position, never assumed. The oracle writes <c>Int32</c>
        ///         where that binds to <c>System.Int32</c> — the implicit <c>using System;</c> is what makes it
        ///         bind in the probe — and <c>System.Int32</c> where something else answers to
        ///         <c>Int32</c>: with a class of that name beside it, the field came back qualified,
        ///         <c>System.Int32</c>. Where neither binds the keyword is kept rather than guessed at:
        ///         being wrong is <c>CS0246</c>, and safety layer 2 would revert the whole document for it.
        ///     </para>
        /// </remarks>
        public override SyntaxNode? VisitPredefinedType(PredefinedTypeSyntax node) {
            var visited = (PredefinedTypeSyntax)base.VisitPredefinedType(node)!;
            var isReceiverOfMemberAccess =
                node.Parent is MemberAccessExpressionSyntax access && access.Expression == node;

            if (isReceiverOfMemberAccess ? options.PredefinedTypeForMemberAccess : options.PredefinedTypeForLocals) {
                return visited;
            }

            // ⚠ The positions the oracle leaves a keyword at `false`, and the ones nobody measured:
            // an enum's underlying type (measured, kept), a using alias and a documentation `cref`.
            if (node.Parent is BaseTypeSyntax { Parent.Parent: EnumDeclarationSyntax }
                || node.Ancestors().Any(static ancestor => ancestor is UsingDirectiveSyntax or CrefSyntax)) {
                return visited;
            }

            if (model.GetTypeInfo(node).Type is not { SpecialType: not SpecialType.None } type
                || Keyword(type) is null
                || FrameworkName(node, type, isReceiverOfMemberAccess) is not { } name) {
                return visited;
            }

            return name.WithLeadingTrivia(visited.GetLeadingTrivia()).WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        /// <summary>
        ///     <c>Int32</c> when that binds to <paramref name="type" /> here, else <c>System.Int32</c> when that
        ///     does, else null.
        /// </summary>
        /// <remarks>
        ///     ⚠ A receiver is bound as an expression, because a local, a parameter or a property named
        ///     <c>Int32</c> captures <c>Int32.MaxValue</c> and none of them is a type. And a receiver gets
        ///     the simple name or nothing: <c>System.Int32</c> there is a member access, not a qualified
        ///     name, and the shadowed case was measured only in a declaration.
        /// </remarks>
        NameSyntax? FrameworkName(SyntaxNode node, ITypeSymbol type, bool asExpression) {
            var option = asExpression
                ? SpeculativeBindingOption.BindAsExpression
                : SpeculativeBindingOption.BindAsTypeOrNamespace;

            var simple = SyntaxFactory.IdentifierName(type.MetadataName);
            NameSyntax[] candidates = asExpression
                ? [simple]
                : [simple, SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), simple)];

            foreach (var candidate in candidates) {
                var bound = model.GetSpeculativeSymbolInfo(node.SpanStart, candidate, option);
                if (bound.CandidateSymbols.IsEmpty && SymbolEqualityComparer.Default.Equals(bound.Symbol, type)) {
                    return candidate;
                }
            }

            return null;
        }

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
                || IsInNameOf(original)) {
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
