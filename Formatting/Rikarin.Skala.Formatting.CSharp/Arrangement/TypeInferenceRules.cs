using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>List&lt;int&gt; x = new List&lt;int&gt;()</c> ⇒ <c>var x = new List&lt;int&gt;()</c>.
/// </summary>
/// <remarks>
/// ⚠ This rule and <see cref="ObjectCreationRule"/> both want the same declaration and only one of
/// them may have it. docs/plan/06 § "Type inference and target typing" states the precedence as
/// "<c>var</c> wins when the RHS names the type; target-typed <c>new</c> wins when the LHS names
/// it", and the measurement agrees with a sharper edge than the prose: for a *local declaration with
/// an initializer* the oracle applies <c>var</c> and then target-typed <c>new</c> has no left-hand
/// type left to target, so it never fires. Target-typed <c>new</c> fires where <c>var</c> cannot
/// reach — a field, a return, an argument, a property initialiser. Ordering the two rules with
/// <c>var</c> first is therefore not a tie-break, it is the whole rule.
/// </remarks>
public sealed class VarRule : ArrangementRule {
    public override string Id => ArrangeIds.Var;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.VarForBuiltInTypes || options.VarWhenTypeIsApparent || options.VarElsewhere;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(SemanticModel model, ArrangementOptions options) : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node) {
            var visited = (VariableDeclarationSyntax)base.VisitVariableDeclaration(node)!;
            return ShouldConvert(node) ? visited.WithType(Var(node.Type)) : visited;
        }

        /// <summary>
        /// ⚠ Reads the *original* node, never the visited one. The visited node's children have been
        /// rebuilt and are not in the tree the semantic model was created for, so asking the model
        /// about them throws. Every semantic rule in this file has the same shape for that reason.
        /// </summary>
        bool ShouldConvert(VariableDeclarationSyntax node) {
            if (node.Type.IsVar) {
                return false;
            }

            // ⚠ `int a = 1, b = 2;` cannot become `var`: one `var` would have to infer two types,
            // and even when both agree C# forbids it.
            if (node.Variables.Count != 1) {
                return false;
            }

            var declarator = node.Variables[0];
            if (declarator.Initializer is not { } initializer) {
                return false;
            }

            // A declaration statement is the only place `var` is legal. `for (int i = 0; …)` is one
            // too, but `foreach` and `using` declarations bind their own way and are left alone.
            if (node.Parent is not (LocalDeclarationStatementSyntax or ForStatementSyntax)) {
                return false;
            }

            // ⚠ `const var` is not a thing — CS0822, "Implicitly-typed variables cannot be
            // constant". Found by safety layer 2 over corpus/real/, on six files, before it was
            // found by reading.
            if (node.Parent is LocalDeclarationStatementSyntax declaration
                && (declaration.Modifiers.Any(SyntaxKind.ConstKeyword)
                    || declaration.UsingKeyword != default)) {
                return false;
            }

            // ⚠ `var x = null` and `var x = () => …` do not compile: the initializer has no type of
            // its own to infer. So does a stackalloc in a non-`Span` context, and a method group.
            var initialiserType = model.GetTypeInfo(initializer.Value).Type;
            if (initialiserType is null
                || initialiserType.TypeKind == TypeKind.Error
                || initialiserType.SpecialType == SpecialType.System_Void) {
                return false;
            }

            // ⚠ The precondition that matters, and the one that makes this rule safe: the declared
            // type and the initializer's own type must be *identical*. `IEnumerable<int> x = list;`
            // is not a `var` candidate — converting it changes the static type of `x` and every
            // overload resolved through it. Layer 3 would catch it; layer 1 is supposed to mean it
            // never gets there.
            //
            // ⚠ IncludeNullability, not Default, and the difference is not pedantry.
            // SymbolEqualityComparer.Default considers `string` and `string?` the same symbol, so
            // `string name = MaybeNull();` looked like a safe conversion and became
            // `var name = MaybeNull();` — which types `name` as `string?` and changes what the
            // nullable analysis concludes about every later use of it. Measured over corpus/real/:
            // this alone produced CS8600 on three files, and made Skala convert declarations the
            // oracle correctly left alone.
            var declaredType = model.GetTypeInfo(node.Type).Type;
            if (declaredType is null
                || !SymbolEqualityComparer.IncludeNullability.Equals(declaredType, initialiserType)) {
                return false;
            }

            // ⚠ An anonymous type already has to be `var`; a pointer never may be.
            if (initialiserType.IsAnonymousType
                || initialiserType.TypeKind is TypeKind.Pointer or TypeKind.Dynamic) {
                return false;
            }

            // ⚠ `Span<byte> s = stackalloc byte[n];` is not `var s = stackalloc byte[n];`. The
            // declared type is what makes the stackalloc a span; without it the natural type is
            // `byte*`, which needs an unsafe context and no longer converts to what the next call
            // expects. Layer 2 found this as CS9360 and CS1503 on two files.
            if (initializer.Value is StackAllocArrayCreationExpressionSyntax
                or ImplicitStackAllocArrayCreationExpressionSyntax) {
                return false;
            }

            // `var x = default;` and `var x = new();` do not compile — the very rewrites
            // ObjectCreationRule and DefaultValueRule perform have to not have happened yet.
            if (initializer.Value is ImplicitObjectCreationExpressionSyntax
                or LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression }) {
                return false;
            }

            return Applies(node.Type, initializer.Value, initialiserType);
        }

        /// <summary>Which of the three <c>csharp_style_var_*</c> keys governs this declaration.</summary>
        bool Applies(TypeSyntax declared, ExpressionSyntax value, ITypeSymbol type) {
            if (declared is PredefinedTypeSyntax || type.SpecialType != SpecialType.None) {
                return options.VarForBuiltInTypes;
            }

            // "Apparent" is ReSharper's word for "the right-hand side names the type": a `new T()`,
            // a cast, an `as`, or a `T.Parse`-shaped call on the same type.
            return IsApparent(value) ? options.VarWhenTypeIsApparent : options.VarElsewhere;
        }

        static bool IsApparent(ExpressionSyntax value) =>
            value is ObjectCreationExpressionSyntax
                or ArrayCreationExpressionSyntax
                or CastExpressionSyntax
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression };

        /// <summary>⚠ Carries the declared type's trivia, so a comment before it survives.</summary>
        static IdentifierNameSyntax Var(TypeSyntax replaced) =>
            SyntaxFactory.IdentifierName("var")
                .WithLeadingTrivia(replaced.GetLeadingTrivia())
                .WithTrailingTrivia(replaced.GetTrailingTrivia());
    }
}

/// <summary>
/// <c>SomeType x = new SomeType()</c> ⇒ <c>SomeType x = new()</c>, where <c>var</c> did not reach.
/// </summary>
public sealed class ObjectCreationRule : ArrangementRule {
    public override string Id => ArrangeIds.ObjectCreation;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.ObjectCreationWhenTypeEvident == ObjectCreationStyle.TargetTyped
        || options.ObjectCreationWhenTypeNotEvident == ObjectCreationStyle.TargetTyped;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(SemanticModel model, ArrangementOptions options) : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (!ShouldConvert(node)) {
                return visited;
            }

            return SyntaxFactory.ImplicitObjectCreationExpression(
                    SyntaxFactory.Token(SyntaxKind.NewKeyword),
                    visited.ArgumentList ?? SyntaxFactory.ArgumentList(),
                    visited.Initializer
                )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        bool ShouldConvert(ObjectCreationExpressionSyntax node) {
            // ⚠ `new T { … }` with no argument list becomes `new() { … }`, which is legal; but
            // `new T[]`-shaped and anonymous creations are other node kinds and never reach here.
            var created = model.GetTypeInfo(node).Type;
            if (created is null || created.TypeKind == TypeKind.Error || created.IsAnonymousType) {
                return false;
            }

            if (TargetTypeOf(node) is not { } target) {
                return false;
            }

            // The whole precondition: the target type must be exactly what `new T()` constructs. A
            // target that is a base class, an interface, `dynamic`, or `var` cannot carry the
            // construction — `IList<int> x = new();` does not compile.
            if (!SymbolEqualityComparer.Default.Equals(target, created)) {
                return false;
            }

            if (target.TypeKind is TypeKind.Interface or TypeKind.Dynamic or TypeKind.TypeParameter) {
                return false;
            }

            return Evident(node)
                ? options.ObjectCreationWhenTypeEvident == ObjectCreationStyle.TargetTyped
                : options.ObjectCreationWhenTypeNotEvident == ObjectCreationStyle.TargetTyped;
        }

        /// <summary>
        /// The type the surrounding syntax imposes, or null when nothing does.
        /// </summary>
        /// <remarks>
        /// ⚠ Deliberately a short, explicit list rather than
        /// <c>GetTypeInfo(node).ConvertedType</c>. The converted type of `new Foo()` in a context
        /// with no target is `Foo` itself, so trusting it would report "the target is Foo" for every
        /// creation everywhere and convert expressions that have no target at all —
        /// <c>Console.WriteLine(new Foo())</c> would become <c>Console.WriteLine(new())</c>, which
        /// does not compile. Each case below is a place C# actually performs target typing.
        /// </remarks>
        ITypeSymbol? TargetTypeOf(ObjectCreationExpressionSyntax node) {
            switch (node.Parent) {
                // `SomeType x = new SomeType();`
                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                    return declarator.Parent is VariableDeclarationSyntax { Type: { } declared } && !declared.IsVar
                        ? model.GetTypeInfo(declared).Type
                        : null;

                // `SomeType P { get; } = new SomeType();` and a parameter's default.
                case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property }:
                    return model.GetTypeInfo(property.Type).Type;

                // `x = new SomeType();`
                case AssignmentExpressionSyntax assignment when assignment.Right == node:
                    return model.GetTypeInfo(assignment.Left).Type;

                // `SomeType M() => new SomeType();` and `return new SomeType();`
                case ArrowExpressionClauseSyntax arrow:
                    return ReturnTypeOf(arrow.Parent);

                case ReturnStatementSyntax statement:
                    return ReturnTypeOf(EnclosingMember(statement));

                default:
                    return null;
            }
        }

        static SyntaxNode? EnclosingMember(SyntaxNode node) {
            for (var current = node.Parent; current is not null; current = current.Parent) {
                switch (current) {
                    case MethodDeclarationSyntax or PropertyDeclarationSyntax or LocalFunctionStatementSyntax
                        or AccessorDeclarationSyntax:
                        return current;

                    // ⚠ A lambda's return type is inferred from its own body, so a `new` inside one
                    // has no target the enclosing method can supply. Stopping here is the difference
                    // between a correct rewrite and one that reads the wrong method's return type.
                    case AnonymousFunctionExpressionSyntax:
                        return null;
                }
            }

            return null;
        }

        ITypeSymbol? ReturnTypeOf(SyntaxNode? member) =>
            member switch {
                MethodDeclarationSyntax method => model.GetTypeInfo(method.ReturnType).Type,
                LocalFunctionStatementSyntax local => model.GetTypeInfo(local.ReturnType).Type,
                PropertyDeclarationSyntax property => model.GetTypeInfo(property.Type).Type,
                AccessorDeclarationSyntax { Parent.Parent: PropertyDeclarationSyntax property } =>
                    model.GetTypeInfo(property.Type).Type,
                _ => null
            };

        /// <summary>
        /// "Evident" is ReSharper's word for "the reader can see the type without looking anywhere
        /// else" — which, for a creation, means the left-hand side spells it out.
        /// </summary>
        static bool Evident(ObjectCreationExpressionSyntax node) =>
            node.Parent is EqualsValueClauseSyntax or AssignmentExpressionSyntax;
    }
}

/// <summary>
/// <c>default(T)</c> ⇒ <c>default</c>, where the target type says which <c>T</c>.
/// </summary>
public sealed class DefaultValueRule : ArrangementRule {
    public override string Id => ArrangeIds.DefaultValue;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.DefaultValueWhenTypeEvident == DefaultValueStyle.DefaultLiteral
        || options.DefaultValueWhenTypeNotEvident == DefaultValueStyle.DefaultLiteral;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(SemanticModel model, ArrangementOptions options) : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitDefaultExpression(DefaultExpressionSyntax node) {
            var visited = (DefaultExpressionSyntax)base.VisitDefaultExpression(node)!;
            if (!ShouldConvert(node)) {
                return visited;
            }

            return SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression)
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        bool ShouldConvert(DefaultExpressionSyntax node) {
            var written = model.GetTypeInfo(node.Type).Type;
            if (written is null || written.TypeKind == TypeKind.Error) {
                return false;
            }

            // ⚠ docs/plan/06: "`default` requires no ambiguity in overload resolution." The bare
            // literal is typeless, so it is only safe where the language gives it exactly one type
            // to take. In an argument position it does not — `M(default)` may pick a different
            // overload from `M(default(int))` — so an argument is never rewritten. This is the
            // conservative half of the precondition and it costs a few conversions the oracle makes.
            var target = TargetTypeOf(node);
            if (target is null || !SymbolEqualityComparer.Default.Equals(target, written)) {
                return false;
            }

            // Evident where the left-hand side spells the type out, which for `default` is every
            // position this method accepts a target from except a parameter's own default value.
            return node.Parent is EqualsValueClauseSyntax { Parent: ParameterSyntax }
                ? options.DefaultValueWhenTypeNotEvident == DefaultValueStyle.DefaultLiteral
                : options.DefaultValueWhenTypeEvident == DefaultValueStyle.DefaultLiteral;
        }

        ITypeSymbol? TargetTypeOf(DefaultExpressionSyntax node) =>
            node.Parent switch {
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                    declarator.Parent is VariableDeclarationSyntax { Type: { } declared } && !declared.IsVar
                        ? model.GetTypeInfo(declared).Type
                        : null,
                EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } =>
                    model.GetTypeInfo(property.Type).Type,
                EqualsValueClauseSyntax { Parent: ParameterSyntax { Type: { } type } } =>
                    model.GetTypeInfo(type).Type,
                AssignmentExpressionSyntax assignment when assignment.Right == node =>
                    model.GetTypeInfo(assignment.Left).Type,
                _ => null
            };
    }
}
