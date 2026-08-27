using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
/// <c>SK1001</c> — <c>T[] x = new T[] { … }</c> and <c>List&lt;T&gt; x = new List&lt;T&gt; { … }</c>
/// are <c>= […]</c>.
/// </summary>
/// <remarks>
/// ⚠ The general rule is one of the two most likely in this range to be wrong, and the reason is
/// that a collection expression has <b>no natural type</b>: it means whatever its target type says,
/// and in a great many positions there is no target type at all. Roslyn's own analyzers for this
/// have a documented tail of reports in places where <c>[…]</c> does not compile. So this fires only
/// where the target type is <em>written down beside it</em> — a declaration with an explicit type —
/// and the created object's type is that type <b>exactly</b>.
/// <para>
/// ⚠ "Exactly" is doing real work, not being pedantic. <c>object[] a = new string[] { … }</c> is an
/// array of <c>string</c> that a <c>string</c> can be read out of and an <c>int</c> cannot be
/// written into; <c>object[] a = [ … ]</c> is an array of <c>object</c>, and the two differ at run
/// time in a way nothing at the assignment can see. Same for <c>IList&lt;T&gt; x = new
/// List&lt;T&gt;{…}</c>, where <c>[…]</c> is free to pick any implementation it likes.
/// </para>
/// <para>
/// ⚠ Constructor arguments end it too. <c>new List&lt;T&gt;(capacity) { … }</c> carries a decision
/// about allocation that <c>[…]</c> does not preserve.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionExpressionAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.CollectionExpression);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CollectionExpression);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var list = start.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
                start.RegisterSyntaxNodeAction(context => Analyze(context, list), SyntaxKind.VariableDeclarator);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? list) {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer?.Value is not ExpressionSyntax value
            || declarator.Parent is not VariableDeclarationSyntax { Type: { } declaredSyntax } declaration) {
            return;
        }

        // ⚠ `var x = new[] { … }` has no written target type, so `var x = […]` is CS9176. This is
        // the single most likely way to get this rule wrong and it is one property away.
        if (declaredSyntax.IsVar) {
            return;
        }

        // Only a local or a field: a `const` cannot hold one, and a `using` declaration is not a
        // collection.
        if (declaration.Parent is not (LocalDeclarationStatementSyntax or FieldDeclarationSyntax)) {
            return;
        }

        if (declaration.Parent is LocalDeclarationStatementSyntax local
            && (local.IsConst || !local.UsingKeyword.IsKind(SyntaxKind.None))) {
            return;
        }

        if (declaration.Parent is FieldDeclarationSyntax declaredField
            && declaredField.Modifiers.IndexOf(SyntaxKind.ConstKeyword) >= 0) {
            return;
        }

        var elements = Elements(value);
        if (elements is null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var declared = model.GetTypeInfo(declaredSyntax, cancellation).Type;
        var created = model.GetTypeInfo(value, cancellation).Type;
        if (declared is null
            || created is null
            || declared.TypeKind == TypeKind.Error
            || created.TypeKind == TypeKind.Error
            || !SymbolEqualityComparer.Default.Equals(declared, created)) {
            return;
        }

        if (!IsSupportedTarget(declared, list)) {
            return;
        }

        // ⚠ A collection expression is not an expression tree node (CS8640).
        if (NullComparison.InsideExpressionTree(model, value, cancellation)) {
            return;
        }

        var span = TextSpan.FromBounds(value.SpanStart, elements.SpanStart);
        if (RewriteGuards.ContainsCommentOrDirective(value.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(value.SyntaxTree, value.Span),
                FixEdits.Pack(
                    (span, string.Empty),
                    (elements.OpenBraceToken.Span, "["),
                    (elements.CloseBraceToken.Span, "]")
                ),
                "The type is already written on the left, so this is a collection expression: `"
                + declaredSyntax
                + " "
                + declarator.Identifier.ValueText
                + " = ["
                + (elements.Expressions.Count == 0 ? string.Empty : "…")
                + "];`"
            )
        );
    }

    /// <summary>
    /// The initializer's element list, when the creation is one a collection expression reproduces.
    /// </summary>
    /// <remarks>
    /// ⚠ Every element has to be an ordinary expression. A <c>{ k, v }</c> pair — the dictionary
    /// shape — is a <c>ComplexElementInitializerExpression</c> calling a two-argument <c>Add</c>,
    /// and there is no collection-expression spelling of it; an object initializer
    /// (<c>{ Count = 3 }</c>) is not element syntax at all. Both are ruled out here rather than by
    /// the type check, because <c>List&lt;T&gt;</c> admits the second.
    /// </remarks>
    static InitializerExpressionSyntax? Elements(ExpressionSyntax value) {
        var initializer = value switch {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
            ObjectCreationExpressionSyntax { ArgumentList: null or { Arguments.Count: 0 } } creation
                => creation.Initializer,
            ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } target
                => target.Initializer,
            _ => null
        };

        if (initializer is null || !initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
            && !initializer.IsKind(SyntaxKind.ArrayInitializerExpression)) {
            return null;
        }

        foreach (var element in initializer.Expressions) {
            if (element is InitializerExpressionSyntax or AssignmentExpressionSyntax) {
                return null;
            }
        }

        return initializer;
    }

    /// <summary>
    /// ⚠ Arrays and <c>List&lt;T&gt;</c> only, and only as the declared type itself.
    /// </summary>
    /// <remarks>
    /// These are the two targets whose collection-expression lowering is specified to produce the
    /// same object the constructor did: a <c>T[]</c> of the same length, or a <c>List&lt;T&gt;</c>
    /// built by the same <c>Add</c> calls. Every other target type — an interface, a builder-attributed
    /// type, a span — is a different object with a different identity, and identity is observable.
    /// </remarks>
    static bool IsSupportedTarget(ITypeSymbol declared, INamedTypeSymbol? list) =>
        declared switch {
            IArrayTypeSymbol { IsSZArray: true } => true,
            INamedTypeSymbol named when list is not null
                => SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, list),
            _ => false
        };
}
