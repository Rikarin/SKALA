using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1131</c> — <c>delegate(int a, int b) { … }</c> where C# 3 spells
///     <c>(int a, int b) =&gt; { … }</c>.
/// </summary>
/// <remarks>
///     <para>
///         The fix is two edits and touches neither the parameters nor the body: the <c>delegate</c>
///         keyword is deleted, and <c>=&gt;</c> is inserted after the parameter list. Everything a
///         model could get wrong about the body — captures of <c>this</c> and <c>base</c>, a
///         <c>goto</c> label, an <c>unsafe</c> pointer parameter, <c>ref</c>/<c>out</c>/<c>in</c>/
///         <c>scoped</c> modifiers, <c>async</c> and <c>static</c> — was compiled and run both ways and
///         agrees, because the lambda keeps the very same parameter list and block.
///     </para>
///     <para>
///         ⚠ <b>The rewrite is not inert, and the difference is overload resolution, not the body.</b>
///         A lambda is <em>applicable</em> to an <c>Expression&lt;TDelegate&gt;</c> parameter; an
///         anonymous method is not. With <c>Q(Func&lt;int, bool&gt;)</c> and
///         <c>Q(Expression&lt;Func&lt;int, bool&gt;&gt;)</c> the anonymous method binds to the first and
///         the lambda is <c>CS0121</c>. Worse, across two static classes: on an
///         <c>IQueryable&lt;int&gt;</c>, <c>q.Where(delegate(int a) { … })</c> binds to
///         <c>Enumerable.Where</c> and runs, while <c>q.Where((int a) =&gt; { … })</c> picks
///         <c>Queryable.Where</c> and is <c>CS0834</c>. Both were compiled, not reasoned about, so every
///         call that encloses the anonymous method is asked for its whole candidate set — reduced
///         extension methods included — and any expression-tree type in any parameter of any candidate
///         withdraws the finding. That is also why <c>fixIsSafe</c> is false.
///     </para>
///     <para>
///         ⚠ <b>A lambda is not a primary expression, and an anonymous method is.</b>
///         <c>b ?? delegate(int x) { … }</c>, <c>(Func&lt;int, int&gt;)delegate(int x) { … }</c> and
///         <c>a + delegate(int x) { … }</c> all compile and all become parse errors as lambdas. The rule
///         fires only in a position a lambda may stand in without parentheses, by an allow-list.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AnonymousMethodWithParameterListAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AnonymousMethodWithParameterList);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AnonymousMethodExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var method = (AnonymousMethodExpressionSyntax)context.Node;

        // `delegate { … }` converts to a delegate of any signature, so a lambda would have to
        // synthesise the target's parameters. Out of scope, not merely excluded.
        if (method.ParameterList is not { } parameters || method.ContainsDiagnostics) {
            return;
        }

        if (!HasOnlyPlainParameters(parameters) || !IsLambdaPosition(method)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // `Expression<Func<int, int>> e = delegate(int x) { … };` is CS1946, and the lambda is CS0834:
        // neither compiles, and a rule has no business trading one error for another.
        if (model.GetTypeInfo(method, cancellation).ConvertedType is not { TypeKind: not TypeKind.Error } converted
            || NullComparison.IsExpressionTreeType(converted)
            || NullComparison.InsideExpressionTree(model, method, cancellation)
            || MayBindToAnExpressionTree(model, method, cancellation)) {
            return;
        }

        var keyword = method.DelegateKeyword;
        var removed = TextSpan.FromBounds(keyword.SpanStart, parameters.SpanStart);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(method.SyntaxTree, removed)) {
            return;
        }

        // The arrow goes immediately after `)`, ahead of whatever trivia follows it, so a comment
        // between the list and the block stays where it was; the insertion deletes nothing and so
        // needs no guard of its own.
        var close = parameters.CloseParenToken;
        var spaced = close.HasTrailingTrivia || method.Block.OpenBraceToken.HasLeadingTrivia;
        var arrow = spaced ? " =>" : " => ";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(method.SyntaxTree, TextSpan.FromBounds(keyword.SpanStart, parameters.Span.End)),
                FixEdits.Pack((removed, string.Empty), (new TextSpan(parameters.Span.End, 0), arrow)),
                "The anonymous method is a lambda: `" + RewriteGuards.Trim(parameters + " => { … }") + "`"
            )
        );
    }

    /// <summary>
    ///     ⚠ An attribute, a default value or <c>params</c> on an anonymous method's parameter is a
    ///     compile error at every language version — <c>CS7014</c>, <c>CS1065</c>, <c>CS1670</c> — and
    ///     each is legal on a lambda from C# 10 or 12.
    /// </summary>
    /// <remarks>
    ///     So the rewrite would turn a program that does not compile into one that does and means
    ///     something nobody wrote. The proposal expected these to need a C# 12 floor; measured, they
    ///     cannot appear in compiling code at all, and this guard is what keeps the rule from repairing
    ///     an error by accident.
    /// </remarks>
    static bool HasOnlyPlainParameters(ParameterListSyntax parameters) {
        foreach (var parameter in parameters.Parameters) {
            if (parameter.Type is null || parameter.AttributeLists.Count > 0 || parameter.Default is not null) {
                return false;
            }

            foreach (var modifier in parameter.Modifiers) {
                if (modifier.IsKind(SyntaxKind.ParamsKeyword) || modifier.IsKind(SyntaxKind.ThisKeyword)) {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Whether a lambda may stand exactly where the anonymous method stands.</summary>
    /// <remarks>
    ///     ⚠ An allow-list, because the failure is a parse error: an anonymous method is a primary
    ///     expression and a lambda is not, so <c>x ?? delegate(…) { … }</c>, a cast operand and a binary
    ///     operand all compile as written and stop parsing once rewritten. Every position below was
    ///     compiled and run both ways.
    /// </remarks>
    static bool IsLambdaPosition(AnonymousMethodExpressionSyntax method) =>
        method.Parent switch {
            ArgumentSyntax
                or EqualsValueClauseSyntax
                or ReturnStatementSyntax
                or ArrowExpressionClauseSyntax
                or ParenthesizedExpressionSyntax
                or ExpressionElementSyntax
                or InitializerExpressionSyntax => true,
            YieldStatementSyntax yield => yield.IsKind(SyntaxKind.YieldReturnStatement),
            AssignmentExpressionSyntax assignment => assignment.Right == method,
            ConditionalExpressionSyntax conditional => conditional.Condition != method,
            SwitchExpressionArmSyntax arm => arm.Expression == method,
            LambdaExpressionSyntax lambda => lambda.ExpressionBody == method,
            _ => false
        };

    /// <summary>
    ///     ⚠ Whether any call enclosing the anonymous method has a candidate that takes an expression
    ///     tree, so that a lambda could bind differently.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The walk climbs to the nearest call whose overload resolution the anonymous method takes
    ///         part in, and stops there. It climbs through a conditional, parentheses, a <c>switch</c>
    ///         arm, a tuple element, a lambda's expression body and a lambda's <c>return</c> — each a
    ///         position whose type the anonymous method helps decide — and stops at any other statement,
    ///         where a declaration or an assignment has already fixed the target type.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The nearest call, not every enclosing one, and that was measured.</b>
    ///         <c>R(Wrap(delegate(int x) { … }))</c>, with <c>R</c> overloaded on <c>Func</c> and
    ///         <c>Expression</c> and a generic <c>Wrap&lt;T&gt;(T)</c>, binds the same both ways:
    ///         <c>Wrap</c> infers <c>T</c> from the natural type, which is a delegate type for both
    ///         spellings, so <c>R</c> never sees a lambda.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The member group, not the bound symbol.</b> The anonymous method's call already
    ///         bound to the delegate overload; the question is what else the lambda would be applicable
    ///         to. <c>GetMemberGroup</c> includes reduced extension methods from every imported static
    ///         class, which is what reaches <c>Queryable.Where</c> from <c>Enumerable.Where</c>.
    ///     </para>
    ///     <para>
    ///         A host whose candidates cannot be enumerated — an indexer, an unbound call — is
    ///         answered "may", which declines.
    ///     </para>
    /// </remarks>
    static bool MayBindToAnExpressionTree(
        SemanticModel model,
        AnonymousMethodExpressionSyntax method,
        CancellationToken cancellation
    ) {
        for (SyntaxNode? current = method; current is not null; current = current.Parent) {
            IEnumerable<ISymbol>? candidates;
            switch (current) {
                case ReturnStatementSyntax ret when EnclosingFunction(ret) is AnonymousFunctionExpressionSyntax lambda:
                    // ⚠ Returned from a lambda, the anonymous method decides that lambda's return type,
                    // and so which overload the lambda's own call picks. Measured: against
                    // `Func<Func<int, bool>>` and `Func<Expression<Func<int, bool>>>`,
                    // `R(() => delegate(int x) { … })` binds and `R(() => (int x) => { … })` is CS0121 —
                    // in an expression body and after `return` alike.
                    current = lambda;
                    continue;
                case MemberDeclarationSyntax or StatementSyntax:
                    return false;
                case ArgumentSyntax { Parent: TupleExpressionSyntax }:
                    // ⚠ Transparent: `Q((1, delegate(int x) { … }))` against a tuple of `Func` and a
                    // tuple of `Expression` binds as written and is CS0121 as a lambda — measured.
                    continue;
                case ArgumentSyntax { Parent: ArgumentListSyntax list }:
                    candidates = ArgumentCandidates(model, list.Parent, cancellation);
                    break;
                case ArgumentSyntax:
                    return true;
                case ExpressionSyntax { Parent: InitializerExpressionSyntax initializer } element
                    when AddHost(initializer) is { } collection:
                    candidates = AddCandidates(model, collection, element, cancellation);
                    break;
                default:
                    continue;
            }

            return candidates is null || candidates.Any(TakesAnExpressionTree);
        }

        return false;
    }

    static SyntaxNode? EnclosingFunction(SyntaxNode node) =>
        node.Ancestors()
            .FirstOrDefault(static ancestor => ancestor is AnonymousFunctionExpressionSyntax
                    or LocalFunctionStatementSyntax
                    or MemberDeclarationSyntax
                    or AccessorDeclarationSyntax
            );

    /// <summary>The initializer whose elements are passed to <c>Add</c>, or null when there is none.</summary>
    static InitializerExpressionSyntax? AddHost(InitializerExpressionSyntax initializer) {
        var collection = initializer;
        if (initializer.IsKind(SyntaxKind.ComplexElementInitializerExpression)
            && initializer.Parent is InitializerExpressionSyntax outer) {
            collection = outer;
        }

        return collection.IsKind(SyntaxKind.CollectionInitializerExpression) ? collection : null;
    }

    static IEnumerable<ISymbol>? ArgumentCandidates(
        SemanticModel model,
        SyntaxNode? host,
        CancellationToken cancellation
    ) {
        switch (host) {
            case InvocationExpressionSyntax invocation: {
                var info = model.GetSymbolInfo(invocation, cancellation);
                var group = model.GetMemberGroup(invocation.Expression, cancellation);
                if (info.Symbol is null && info.CandidateSymbols.IsEmpty && group.IsEmpty) {
                    return null;
                }

                return group.Concat(info.CandidateSymbols).Concat(info.Symbol is null ? [] : [info.Symbol]);
            }
            case BaseObjectCreationExpressionSyntax creation:
                return CreatedType(model, creation, cancellation)?.InstanceConstructors;
            case ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax:
                return model.GetSymbolInfo(host, cancellation) is { Symbol.ContainingType: { } containing }
                    ? containing.InstanceConstructors
                    : null;
            default:
                return null;
        }
    }

    static INamedTypeSymbol? CreatedType(
        SemanticModel model,
        BaseObjectCreationExpressionSyntax creation,
        CancellationToken cancellation
    ) =>
        model.GetTypeInfo(creation, cancellation).Type is INamedTypeSymbol { TypeKind: not TypeKind.Error } type
            ? type
            : null;

    /// <summary>
    ///     A collection initializer's element is an argument to <c>Add</c>, and <c>Add</c> is
    ///     overloadable like any other call.
    /// </summary>
    static IEnumerable<ISymbol>? AddCandidates(
        SemanticModel model,
        InitializerExpressionSyntax collection,
        ExpressionSyntax element,
        CancellationToken cancellation
    ) =>
        collection.Parent is BaseObjectCreationExpressionSyntax creation
        && CreatedType(model, creation, cancellation) is { } type
            ? model.LookupSymbols(element.SpanStart, type, "Add", true)
            : null;

    /// <summary>
    ///     Whether a candidate method has a parameter a lambda could be converted to as a tree.
    /// </summary>
    /// <remarks>
    ///     A candidate that is not a method — a delegate-typed field or property in the group — has
    ///     no parameter list of its own; the call through it is its <c>Invoke</c>, which the bound
    ///     symbol already contributes.
    /// </remarks>
    static bool TakesAnExpressionTree(ISymbol candidate) =>
        candidate is IMethodSymbol method
        && (method.ReducedFrom ?? method).OriginalDefinition.Parameters
        .Any(static parameter => MentionsAnExpressionTree(parameter.Type));

    /// <summary>
    ///     ⚠ Anywhere in the type, not only at its top: <c>params Expression&lt;Func&lt;T, object&gt;&gt;[]</c>
    ///     is how an include list is spelled, and a lambda is applicable to its element.
    /// </summary>
    static bool MentionsAnExpressionTree(ITypeSymbol type) =>
        NullComparison.IsExpressionTreeType(type)
        || type switch {
            IArrayTypeSymbol array => MentionsAnExpressionTree(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(MentionsAnExpressionTree),
            _ => false
        };
}
