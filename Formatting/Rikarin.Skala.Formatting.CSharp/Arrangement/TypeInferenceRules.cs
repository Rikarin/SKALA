using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>List&lt;int&gt; x = new List&lt;int&gt;()</c> ⇒ <c>var x = new List&lt;int&gt;()</c>.
/// </summary>
/// <remarks>
///     ⚠ This rule and <see cref="ObjectCreationRule" /> both want the same declaration and only one of
///     them may have it. docs/plan/06 § "Type inference and target typing" states the precedence as
///     "<c>var</c> wins when the RHS names the type; target-typed <c>new</c> wins when the LHS names
///     it", and the measurement agrees with a sharper edge than the prose: for a *local declaration with
///     an initializer* the oracle applies <c>var</c> and then target-typed <c>new</c> has no left-hand
///     type left to target, so it never fires. Target-typed <c>new</c> fires where <c>var</c> cannot
///     reach — a field, a return, an argument, a property initialiser. Ordering the two rules with
///     <c>var</c> first is therefore not a tie-break, it is the whole rule.
/// </remarks>
public sealed class VarRule : ArrangementRule {
    public override string Id => ArrangeIds.Var;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.VarForBuiltInTypes || options.VarWhenTypeIsApparent || options.VarElsewhere;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node) {
            var visited = (VariableDeclarationSyntax)base.VisitVariableDeclaration(node)!;
            return ShouldConvert(node) ? visited.WithType(Var(node.Type)) : visited;
        }

        /// <summary>
        ///     ⚠ Reads the *original* node, never the visited one. The visited node's children have been
        ///     rebuilt and are not in the tree the semantic model was created for, so asking the model
        ///     about them throws. Every semantic rule in this file has the same shape for that reason.
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
            var info = model.GetTypeInfo(initializer.Value);
            var initialiserType = info.Type;
            if (initialiserType is null
                || initialiserType.TypeKind == TypeKind.Error
                || initialiserType.SpecialType == SpecialType.System_Void) {
                return false;
            }

            // ⚠ `int[] a = { 1, 2 };` is not `var a = { 1, 2 };`. A bare array initializer has no
            // type of its own — the declared type is what gives it one — and dropping it is CS0826,
            // "no best type found for implicitly-typed array". 24 files on Vixen.
            if (initializer.Value is InitializerExpressionSyntax) {
                return false;
            }

            // ⚠ The precondition that CS8600 taught, and it is the subtle one. `var` does not take
            // the initializer's *declared* nullability, it takes its **flow state**: for
            //
            //     string s = MightBeNullHere();
            //
            // where the method's return type is `string` but the flow analysis has concluded
            // maybe-null at this point, `var s = …` gives `s` the type `string?`, and the next place
            // `s` is passed to something expecting `string` is a new CS8600. The declared types are
            // identical, so the equality check below cannot see it. 567 of Vixen's 618 reverts were
            // this one — the largest single cause, and invisible to every check that compares types
            // rather than states.
            if (info.Nullability.FlowState == NullableFlowState.MaybeNull
                && node.Type is not NullableTypeSyntax) {
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
            if (IsBuiltIn(declared) || IsBuiltIn(type)) {
                return options.VarForBuiltInTypes;
            }

            // "Apparent" is ReSharper's word for "the right-hand side names the type": a `new T()`,
            // a cast, an `as`, or a `T.Parse`-shaped call on the same type.
            return IsApparent(value) ? options.VarWhenTypeIsApparent : options.VarElsewhere;
        }

        /// <summary>
        ///     Whether <c>csharp_style_var_for_built_in_types</c> is the key that governs this declared
        ///     type — which is a question about the element type, not about the array wrapping it.
        /// </summary>
        /// <remarks>
        ///     ⚠ Measured, one key at a time with all three restated, on a nine-case probe under the
        ///     cleanup profile. <c>int[]</c>, <c>string[]</c>, <c>int?[]</c>, <c>int[][]</c> and
        ///     <c>int[,]</c> are all declined at <c>for_built_in_types = false</c> and converted at the
        ///     other two keys' <c>false</c>; <c>List&lt;int&gt;[]</c> is the opposite — converted at
        ///     <c>for_built_in_types = false</c> and declined at <c>when_type_is_apparent = false</c>. So
        ///     the array ranks and the nullable wrapper are transparent to the question and the element
        ///     type answers it.
        ///     <para>
        ///         ⚠ Before this, <c>int[] a = new int[4];</c> reached the apparent branch — an
        ///         <c>ArrayTypeSyntax</c> is not a <c>PredefinedTypeSyntax</c> and an array symbol's
        ///         <c>SpecialType</c> is <c>None</c> — and the two keys' sweep rows were mirror images of
        ///         each other on that one line. It is invisible at the export, where all three keys are
        ///         <c>true</c> and every branch returns the same answer.
        ///     </para>
        /// </remarks>
        static bool IsBuiltIn(TypeSyntax declared) =>
            declared switch {
                PredefinedTypeSyntax => true,
                ArrayTypeSyntax array => IsBuiltIn(array.ElementType),
                NullableTypeSyntax nullable => IsBuiltIn(nullable.ElementType),
                _ => false
            };

        /// <summary>
        ///     ⚠ The same question of the symbol, which is what catches a built-in type written under its
        ///     framework name — <c>Int32 a = 1;</c> is a <c>for_built_in_types</c> declaration however it
        ///     is spelled.
        /// </summary>
        static bool IsBuiltIn(ITypeSymbol type) =>
            type is IArrayTypeSymbol array
                ? IsBuiltIn(array.ElementType)
                : type.SpecialType != SpecialType.None;

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
///     <c>SomeType x = new SomeType()</c> ⇒ <c>SomeType x = new()</c>, where <c>var</c> did not reach.
/// </summary>
public sealed class ObjectCreationRule : ArrangementRule {
    public override string Id => ArrangeIds.ObjectCreation;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.ObjectCreationWhenTypeEvident == ObjectCreationStyle.TargetTyped
        || options.ObjectCreationWhenTypeNotEvident == ObjectCreationStyle.TargetTyped;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
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

        bool ShouldConvert(ObjectCreationExpressionSyntax node) =>
            TargetTypeOf(node) is { } target
            && Carries(node, target)
            && (Evident(node)
                    ? options.ObjectCreationWhenTypeEvident == ObjectCreationStyle.TargetTyped
                    : options.ObjectCreationWhenTypeNotEvident == ObjectCreationStyle.TargetTyped);

        /// <summary>Whether <c>new()</c> aimed at <paramref name="target" /> constructs what the creation does.</summary>
        bool Carries(ObjectCreationExpressionSyntax node, ITypeSymbol target) {
            // ⚠ `new T { … }` with no argument list becomes `new() { … }`, which is legal; but
            // `new T[]`-shaped and anonymous creations are other node kinds and never reach here.
            var created = model.GetTypeInfo(node).Type;
            if (created is null || created.TypeKind == TypeKind.Error || created.IsAnonymousType) {
                return false;
            }

            // ⚠ An argument that takes ITS target type from the constructor being called cannot
            // survive the type name being dropped. `new Machine([a, b])` names the type, which is
            // what gives `[a, b]` its own target; `new([a, b])` leaves the collection expression
            // with nothing to be, and the compiler says so — CS8754, "there is no target type for
            // 'new(collection expression)'". The same holds for a nested `new()` or a lambda. Seven
            // files on Vixen, and the last cause of a re-bind revert there.
            foreach (var argument in node.ArgumentList?.Arguments ?? default) {
                if (argument.Expression is CollectionExpressionSyntax
                    or ImplicitObjectCreationExpressionSyntax
                    or ImplicitArrayCreationExpressionSyntax
                    or InitializerExpressionSyntax
                    or AnonymousFunctionExpressionSyntax
                    or LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression }) {
                    return false;
                }
            }

            // The whole precondition: the target type must be exactly what `new T()` constructs. A
            // target that is a base class, an interface, `dynamic`, or `var` cannot carry the
            // construction — `IList<int> x = new();` does not compile.
            if (!SymbolEqualityComparer.Default.Equals(target, created)) {
                return false;
            }

            return target.TypeKind is not (TypeKind.Interface or TypeKind.Dynamic or TypeKind.TypeParameter);
        }

        /// <summary>The arguments of each argument list that may become <c>new()</c>, decided once per list.</summary>
        readonly Dictionary<BaseArgumentListSyntax, Dictionary<ArgumentSyntax, ITypeSymbol>> _arguments = [];

        /// <summary>
        ///     The parameter type an argument's creation is target-typed to, or null when it may not be.
        /// </summary>
        /// <remarks>
        ///     ⚠ An argument is the one target-typed position where dropping the type can change which
        ///     member is called <em>and still compile</em>: <c>new()</c> converts to every type, so
        ///     <c>Take(new())</c> beside <c>Take(object)</c> and <c>Take(string)</c> binds the
        ///     <c>string</c> overload that <c>Take(new object())</c> did not. Safety layer 3 compares the
        ///     name <c>Take</c> before and after and would revert the whole document; this asks first.
        ///     Each candidate is accepted only when the call, re-bound speculatively with it and every
        ///     earlier accepted candidate written <c>new()</c>, still reaches the same member.
        ///     <para>
        ///         ⚠ Left to right and greedy, because that is what the oracle does. Measured against
        ///         <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c> with <c>Cross(Foo, Bar)</c> and
        ///         <c>Cross(Bar, Foo)</c>: <c>Cross(new Foo(), new Bar())</c> becomes
        ///         <c>Cross(new(), new Bar())</c> — the first alone still picks one overload, both together
        ///         would be ambiguous — and <c>Cross(new Bar(), new Foo())</c> becomes
        ///         <c>Cross(new(), new Foo())</c>. Deciding the list all-or-nothing would refuse both.
        ///     </para>
        ///     <para>
        ///         The same probe's other rows, every one of which this reproduces: converted for a single
        ///         overload, overloads of different arity, a named argument out of order, an <c>in</c>
        ///         parameter, an optional one, a nullable-annotated reference, a struct, explicit type
        ///         arguments (<c>Gen&lt;Foo&gt;(new())</c>), a static on a constructed type, an extension
        ///         method, a delegate invocation, an indexer, a constructor's argument, a <c>base(…)</c>,
        ///         <c>this(…)</c> and primary-constructor base; declined for an overload pair the
        ///         conversion would make ambiguous, an inferred type argument (<c>Gen(new Foo())</c>), a
        ///         <c>params</c> parameter in either form, a base-class, interface or <c>Nullable&lt;T&gt;</c>
        ///         parameter, a <c>dynamic</c> receiver and <c>Console.WriteLine(new object())</c>. Governed
        ///         by <c>object_creation_when_type_not_evident</c>: flipping it alone restored every row,
        ///         flipping the evident key moved none.
        ///     </para>
        /// </remarks>
        ITypeSymbol? ArgumentTargetOf(ArgumentSyntax argument, BaseArgumentListSyntax list) {
            if (!_arguments.TryGetValue(list, out var accepted)) {
                accepted = AcceptedArguments(list);
                _arguments[list] = accepted;
            }

            return accepted.GetValueOrDefault(argument);
        }

        Dictionary<ArgumentSyntax, ITypeSymbol> AcceptedArguments(BaseArgumentListSyntax list) {
            var accepted = new Dictionary<ArgumentSyntax, ITypeSymbol>();
            if (list.Parent is not { } owner
                || model.GetSymbolInfo(owner).Symbol is not { } member
                || ParametersOf(member) is not { } parameters) {
                return accepted;
            }

            foreach (var argument in list.Arguments) {
                if (argument.Expression is not ObjectCreationExpressionSyntax creation
                    || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
                    || ParameterOf(argument, list, parameters) is not {
                        IsParams: false, RefKind: RefKind.None or RefKind.In
                    } parameter
                    || !Carries(creation, parameter.Type)) {
                    continue;
                }

                accepted[argument] = parameter.Type;
                if (!SymbolEqualityComparer.Default.Equals(
                        Rebind(owner, accepted.Keys.Select(static accepted => accepted.Expression)),
                        member
                    )) {
                    accepted.Remove(argument);
                }
            }

            return accepted;
        }

        static IReadOnlyList<IParameterSymbol>? ParametersOf(ISymbol member) =>
            member switch {
                IMethodSymbol method => method.Parameters,
                IPropertySymbol { IsIndexer: true } indexer => indexer.Parameters,
                _ => null
            };

        static IParameterSymbol? ParameterOf(
            ArgumentSyntax argument,
            BaseArgumentListSyntax list,
            IReadOnlyList<IParameterSymbol> parameters
        ) {
            if (argument.NameColon is { } name) {
                return parameters.FirstOrDefault(parameter => parameter.Name == name.Name.Identifier.ValueText);
            }

            // ⚠ A positional argument after a named one is legal only in its own position, so the index
            // is the parameter's either way.
            var index = list.Arguments.IndexOf(argument);
            return index < parameters.Count ? parameters[index] : null;
        }

        /// <summary>
        ///     The member <paramref name="owner" /> reaches with <paramref name="converted" /> written
        ///     <c>new()</c>, bound speculatively at the owner's own position; null when it reaches none.
        /// </summary>
        ISymbol? Rebind(SyntaxNode owner, IEnumerable<ExpressionSyntax> converted) {
            var rewritten = owner.ReplaceNodes(
                converted,
                static (original, _) => {
                    var creation = (ObjectCreationExpressionSyntax)original;
                    return SyntaxFactory.ImplicitObjectCreationExpression(
                        SyntaxFactory.Token(SyntaxKind.NewKeyword),
                        creation.ArgumentList ?? SyntaxFactory.ArgumentList(),
                        creation.Initializer
                    );
                }
            );

            // ⚠ `Changed?.Invoke(this, new(…))` binds only with its receiver: the invocation's
            // `.Invoke` is a member binding, which means nothing out of place, and a speculative
            // conditional access answers with no symbol at all. So the statement around it is re-bound
            // instead, and the invocation is asked inside that.
            if (owner.Parent is ConditionalAccessExpressionSyntax) {
                return RebindInStatement(owner, rewritten);
            }

            var info = rewritten switch {
                ConstructorInitializerSyntax initializer => model.GetSpeculativeSymbolInfo(
                    owner.SpanStart,
                    initializer
                ),
                PrimaryConstructorBaseTypeSyntax baseType => model.GetSpeculativeSymbolInfo(owner.SpanStart, baseType),
                // ⚠ A target-typed outer `new(…)` has no type of its own to bind against out of place;
                // its arguments are left as written rather than guessed at.
                ImplicitObjectCreationExpressionSyntax => default,
                ExpressionSyntax expression => model.GetSpeculativeSymbolInfo(
                    owner.SpanStart,
                    expression,
                    SpeculativeBindingOption.BindAsExpression
                ),
                _ => default
            };

            return info.CandidateSymbols.IsEmpty ? info.Symbol : null;
        }

        /// <summary>
        ///     <see cref="Rebind" /> for an owner that cannot be bound out of place, through a speculative
        ///     model of the whole statement holding it; null when it is not in a statement.
        /// </summary>
        ISymbol? RebindInStatement(SyntaxNode owner, SyntaxNode rewritten) {
            if (owner.FirstAncestorOrSelf<StatementSyntax>() is not { } statement) {
                return null;
            }

            var marker = new SyntaxAnnotation();
            var replaced = statement.ReplaceNode(owner, rewritten.WithAdditionalAnnotations(marker));
            if (!model.TryGetSpeculativeSemanticModel(statement.SpanStart, replaced, out var speculative)
                || replaced.GetAnnotatedNodes(marker).FirstOrDefault() is not { } bound) {
                return null;
            }

            var info = speculative.GetSymbolInfo(bound);
            return info.CandidateSymbols.IsEmpty ? info.Symbol : null;
        }

        /// <summary>
        ///     The type the surrounding syntax imposes, or null when nothing does.
        /// </summary>
        /// <remarks>
        ///     ⚠ Deliberately a short, explicit list rather than
        ///     <c>GetTypeInfo(node).ConvertedType</c>. The converted type of `new Foo()` in a context
        ///     with no target is `Foo` itself, so trusting it would report "the target is Foo" for every
        ///     creation everywhere and convert expressions that have no target at all —
        ///     <c>Console.WriteLine(new Foo())</c> would become <c>Console.WriteLine(new())</c>, which
        ///     does not compile. Each case below is a place C# actually performs target typing.
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
                //
                // ⚠ Simple assignment only. `x += new Vector3(…)` is an AssignmentExpression too,
                // and its right-hand side is an operand of `operator +`, not a target-typed
                // position: `x += new(…)` is CS8310, "operator '+=' cannot be applied to operand
                // 'new(float, float, float)'". Nine files on Vixen.
                // ⚠ And not a discard. `_ = new Regex(p, o)` type-infers its left from the RIGHT, so
                // `GetTypeInfo` answers `Regex` and the precondition below passes — but `_ = new(p, o)`
                // is `CS8754: There is no target type for 'new(...)'`. A discard imposes nothing; it
                // takes whatever it is given. Asked semantically rather than by spelling, because a
                // local genuinely named `_` is a real target.
                case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
                    when assignment.Right == node
                    && model.GetSymbolInfo(assignment.Left).Symbol is not IDiscardSymbol:
                    return model.GetTypeInfo(assignment.Left).Type;

                // `SomeType M() => new SomeType();` and `return new SomeType();`
                case ArrowExpressionClauseSyntax arrow:
                    return ReturnTypeOf(arrow.Parent);

                case ReturnStatementSyntax statement:
                    return EnclosingMember(statement) is AnonymousFunctionExpressionSyntax function
                        ? FunctionReturnTargetOf(function, node)
                        : ReturnTypeOf(EnclosingMember(statement));

                // `Take(() => new Foo())` (#524).
                case LambdaExpressionSyntax lambda when lambda.ExpressionBody == node:
                    return FunctionReturnTargetOf(lambda, node);

                // ⚠ An element of a collection or array initializer. This is the one place the
                // *converted* type is a real target rather than a restatement of the expression's
                // own type: the element type is imposed by the collection being built, so
                // `new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("a", 1) }`
                // becomes `{ new("a", 1) }`. Found on Newtonsoft's tests, where the oracle does it
                // and Skala did not — the shape is common enough in test data to be worth the case.
                //
                // ⚠ Still gated by `target == created` in the caller, which is what keeps it safe: a
                // collection whose `Add` takes a base type reports that base as the converted type,
                // the equality fails, and the creation is left explicit.
                //
                // ⚠ And only when the collection itself is EXPLICITLY typed. `new[] { new Foo() }`
                // infers its element type *from the elements*, so target-typing them is circular:
                // `new[] { new() }` is CS0826, "no best type found for implicitly-typed array". The
                // same holds for a collection expression `[new Foo()]`, which is CS8754. Twelve and
                // two files on Vixen respectively, and both were found by the re-bind rather than by
                // reasoning about the case.
                case InitializerExpressionSyntax {
                    RawKind:
                    (int)SyntaxKind.CollectionInitializerExpression
                        or (int)SyntaxKind.ArrayInitializerExpression,
                    Parent: ObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax
                }:
                    return model.GetTypeInfo(node).ConvertedType;

                // `Take(new Foo())`, `base(new Foo())`, `map[new Foo()]` (#461).
                case ArgumentSyntax { Parent: BaseArgumentListSyntax list } argument:
                    return ArgumentTargetOf(argument, list);

                default:
                    return null;
            }
        }

        static SyntaxNode? EnclosingMember(SyntaxNode node) {
            for (var current = node.Parent; current is not null; current = current.Parent) {
                switch (current) {
                    case MethodDeclarationSyntax
                        or PropertyDeclarationSyntax
                        or LocalFunctionStatementSyntax
                        or AccessorDeclarationSyntax:
                        return current;

                    // ⚠ A `return` inside a lambda returns from the lambda, never from the enclosing
                    // method: reading the method's return type here is the wrong method's answer. The
                    // lambda itself is handed back, and FunctionReturnTargetOf decides whether its
                    // return type is fixed by its context or inferred from this very body.
                    case AnonymousFunctionExpressionSyntax:
                        return current;
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
        ///     "Evident" is ReSharper's word for "the reader can see the type without looking anywhere
        ///     else" — which means the type is written in the syntax that gives this creation its target.
        /// </summary>
        /// <remarks>
        ///     ⚠ An assignment is <b>not</b> evident and a <c>return</c> or arrow body <b>is</b>, which is
        ///     the opposite of what this method said until it was measured. Asked one key at a time on
        ///     <c>type-inference/new-wins-when-lhs-names-the-type.cs</c> under the cleanup profile, the two
        ///     keys' rows were exact mirror images:
        ///     <code>
        /// when_type_evident = explicitly_typed      Make() =&gt; new List&lt;int&gt;()   local = new()
        /// when_type_not_evident = explicitly_typed  Make() =&gt; new()               local = new List&lt;int&gt;()
        ///     </code>
        ///     A declarator and a field or property initializer write the type beside the creation, and a
        ///     <c>return</c> or arrow body has it in the member's own header, so all of those are evident;
        ///     <c>local = new()</c> is an assignment to a name declared somewhere else, so it is not. Two
        ///     mirrored rows are what a swapped classification produces, and nothing else on the file
        ///     moved.
        ///     <para>
        ///         ⚠ Invisible at the export, where both keys are <c>target_typed</c> and the branch does not
        ///         matter. That is why it survived: the committed fixture pins one configuration.
        ///     </para>
        /// </remarks>
        static bool Evident(ObjectCreationExpressionSyntax node) =>
            node.Parent is EqualsValueClauseSyntax or ArrowExpressionClauseSyntax
            // ⚠ A lambda's `return` is not evident (#524, measured): the type is in a delegate somewhere
            // else, not in the header of anything the reader is looking at.
            || node.Parent is ReturnStatementSyntax
            && EnclosingMember(node) is not AnonymousFunctionExpressionSyntax;

        /// <summary>
        ///     The type a <c>new</c> returned from a lambda or an anonymous method is target-typed to, or null
        ///     when it may not be.
        /// </summary>
        /// <remarks>
        ///     ⚠ #524, measured against <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c>. The oracle
        ///     writes <c>new()</c> for the value of a lambda — arrow or block <c>return</c>, an anonymous
        ///     method, <c>async</c> (through <c>Task&lt;Foo&gt;</c>), an <c>Expression&lt;Func&lt;Foo&gt;&gt;</c>
        ///     — whenever the delegate's return type is fixed from outside the lambda: an argument whose
        ///     call still binds the same member (<c>TakeFunc</c>, <c>Gen&lt;Foo&gt;</c>), a field, a property
        ///     arrow, an assignment. It declines <c>Over(Func&lt;Foo&gt;)</c> beside
        ///     <c>Over(Func&lt;Bar&gt;)</c>, an inferred <c>Gen(() =&gt; new Foo())</c>, <c>Task.Run</c>,
        ///     <c>Func&lt;object&gt;</c>, and <c>var f = () =&gt; new Foo()</c>, whose type comes from the very
        ///     body being rewritten. Governed by <c>when_type_not_evident</c>, measured: flipping it alone
        ///     restored every row, the block-bodied <c>return</c> included.
        ///     <para>
        ///         ⚠ The argument case is #461's check verbatim: the call is re-bound speculatively with the
        ///         creation written <c>new()</c> and must reach the same member. A lambda's return type takes
        ///         part in overload resolution and type inference, so this is the same hazard, one level down.
        ///     </para>
        /// </remarks>
        ITypeSymbol? FunctionReturnTargetOf(
            AnonymousFunctionExpressionSyntax function,
            ObjectCreationExpressionSyntax node
        ) {
            if (!ContextFixesTheType(function, node)) {
                return null;
            }

            var converted = model.GetTypeInfo(function).ConvertedType as INamedTypeSymbol;
            if (converted is { Name: "Expression", TypeArguments: [INamedTypeSymbol inner] }
                && converted.ContainingNamespace?.ToDisplayString() == "System.Linq.Expressions") {
                converted = inner;
            }

            if (converted is not { TypeKind: TypeKind.Delegate, DelegateInvokeMethod.ReturnType: { } returned }) {
                return null;
            }

            if (!function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword)) {
                return returned;
            }

            // An `async` lambda returns the task's result, and only the two task types are asked about.
            return returned is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments: [{ } result] } task
                && task.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"
                    ? result
                    : null;
        }

        /// <summary>Whether the lambda's delegate type is imposed from outside rather than read off its body.</summary>
        bool ContextFixesTheType(AnonymousFunctionExpressionSyntax function, ObjectCreationExpressionSyntax node) {
            switch (function.Parent) {
                case ArgumentSyntax { Parent: BaseArgumentListSyntax { Parent: { } owner } }:
                    return model.GetSymbolInfo(owner).Symbol is { } member
                        && SymbolEqualityComparer.Default.Equals(Rebind(owner, [node]), member);

                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                    return declarator.Parent is VariableDeclarationSyntax { Type.IsVar: false };

                case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax }:
                case ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax or MethodDeclarationSyntax }:
                case CastExpressionSyntax:
                    return true;

                case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment:
                    return assignment.Right == function
                        && model.GetSymbolInfo(assignment.Left).Symbol is not IDiscardSymbol;

                default:
                    return false;
            }
        }
    }
}

/// <summary>
///     <c>default(T)</c> ⇒ <c>default</c>, where the target type says which <c>T</c>.
/// </summary>
public sealed class DefaultValueRule : ArrangementRule {
    public override string Id => ArrangeIds.DefaultValue;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.DefaultValueWhenTypeEvident == DefaultValueStyle.DefaultLiteral
        || options.DefaultValueWhenTypeNotEvident == DefaultValueStyle.DefaultLiteral;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
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

            // ⚠ Evident where the type is written in the syntax that gives the literal its target, and
            // an assignment is the one position this method accepts a target from where it is not.
            // Measured one key at a time on `type-inference/default-literal.cs` under the cleanup
            // profile, and the two keys' rows came back exact mirror images:
            //
            //   when_type_evident = default_expression      Held = default          count = default(int)
            //   when_type_not_evident = default_expression   Held = default(List<int>)  count = default
            //
            // So a *parameter's own default* is evident — the type is on the parameter beside it — and
            // `Held = default` is not, because `Held` is declared elsewhere. This method said the
            // opposite and so did `default-literal.cs`'s header comment; both are corrected. Invisible
            // at the export, where both keys are `default_literal`.
            return node.Parent is AssignmentExpressionSyntax
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
