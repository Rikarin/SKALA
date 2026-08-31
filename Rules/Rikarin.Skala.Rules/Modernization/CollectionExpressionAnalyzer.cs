using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1001</c> — <c>T[] x = new T[] { … }</c> and <c>List&lt;T&gt; x = new List&lt;T&gt; { … }</c>
///     are <c>= […]</c>.
/// </summary>
/// <remarks>
///     ⚠ The general rule is one of the two most likely in this range to be wrong, and the reason is
///     that a collection expression has <b>no natural type</b>: it means whatever its target type says,
///     and in a great many positions there is no target type at all. Roslyn's own analyzers for this
///     have a documented tail of reports in places where <c>[…]</c> does not compile. So this fires only
///     where the target type is <em>written down beside it</em> — a declaration with an explicit type —
///     and the created object's type is that type <b>exactly</b>.
///     <para>
///         ⚠ "Exactly" is doing real work, not being pedantic. <c>object[] a = new string[] { … }</c> is an
///         array of <c>string</c> that a <c>string</c> can be read out of and an <c>int</c> cannot be
///         written into; <c>object[] a = [ … ]</c> is an array of <c>object</c>, and the two differ at run
///         time in a way nothing at the assignment can see. Same for
///         <c>
/// IList&lt;T&gt; x = new
///  List&lt;T&gt;{…}
///         </c>, where <c>[…]</c> is free to pick any implementation it likes.
///     </para>
///     <para>
///         ⚠ Constructor arguments end it too. <c>new List&lt;T&gt;(capacity) { … }</c> carries a decision
///         about allocation that <c>[…]</c> does not preserve.
///     </para>
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
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, list),
                    SyntaxKind.ArrayCreationExpression,
                    SyntaxKind.ImplicitArrayCreationExpression,
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? list) {
        var value = (ExpressionSyntax)context.Node;
        var elements = Elements(value);
        if (elements is null) {
            return;
        }

        var target = WrittenTargetOf(value);
        if (target is null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var info = model.GetTypeInfo(value, cancellation);

        // ⚠ The whole proof, in one comparison. A collection expression produces an object of its
        // *target* type; the `new` produced an object of its own. Where the two are the same the
        // rewrite cannot change what the program holds, and where they differ it silently can:
        // `object[] a = new string[] { … }` is an array of `string` that throws on an `int` write,
        // and `object[] a = […]` is an array of `object` that does not. Asking Roslyn for the
        // converted type catches every such widening at once, including the ones through an
        // interface and the ones through a base class.
        if (info.Type is not { } created
            || created.TypeKind == TypeKind.Error
            || info.ConvertedType is not { } converted
            || !SymbolEqualityComparer.Default.Equals(created, converted)) {
            return;
        }

        if (!IsSupportedTarget(created, list)) {
            return;
        }

        // ⚠ A collection expression is not an expression tree node.
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
                "The type is already written at "
                + target
                + ", so this is a collection expression: `"
                + created.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                + (elements.Expressions.Count == 0 ? " x = [];`" : " x = […];`")
            )
        );
    }

    /// <summary>
    ///     Where the target type is written down, or null when it is not written anywhere.
    /// </summary>
    /// <remarks>
    ///     ⚠ A collection expression has no natural type: it means whatever the position says it means,
    ///     and a position that says nothing is CS9176. So the rule needs the type to be
    ///     <em>
    ///         spelled by
    ///         the author
    ///     </em> somewhere the reader can see, and this is the list of places where it is.
    ///     <para>
    ///         ⚠ An argument is deliberately not on the list even though the parameter's type is written.
    ///         `M(new string[] { … })` and `M([…])` do not necessarily resolve to the same overload — a
    ///         collection expression is convertible to several collection types at once — so the rewrite
    ///         can change which method runs.
    ///     </para>
    /// </remarks>
    static string? WrittenTargetOf(ExpressionSyntax value) {
        switch (value.Parent) {
            // `T[] x = new T[] { … };` — but never `var`, which has nothing to infer from.
            case EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            }:
                if (declaration.Type.IsVar
                    || declaration.Parent is not (LocalDeclarationStatementSyntax or FieldDeclarationSyntax)
                    || declaration.Parent is LocalDeclarationStatementSyntax { IsConst: true }
                    || declaration.Parent is FieldDeclarationSyntax declaredField
                    && declaredField.Modifiers.IndexOf(SyntaxKind.ConstKeyword) >= 0) {
                    return null;
                }

                return "the declaration";

            // `return new T[] { … };` in a member whose return type is written out.
            case ReturnStatementSyntax statement:
                return HasWrittenReturnType(statement) ? "the return type" : null;

            // `x = new T[] { … };` where `x` already has a type.
            case AssignmentExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression
            } assignment when ReferenceEquals(assignment.Right, value)
                && RewriteGuards.IsPlainNamePath(assignment.Left):
                return "the assignment target";

            default:
                return null;
        }
    }

    /// <summary>
    ///     ⚠ The enclosing member's return type has to be written, not inferred.
    /// </summary>
    /// <remarks>
    ///     A lambda's return type is inferred from its body, so `Func&lt;string[]&gt; f = () =&gt; […]`
    ///     has nothing to take a target type from. Walking out to the first member declaration and
    ///     stopping at any lambda in between is how that is asked.
    /// </remarks>
    static bool HasWrittenReturnType(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                    return false;

                case LocalFunctionStatementSyntax function:
                    return !function.ReturnType.IsVar;

                case MethodDeclarationSyntax method:
                    return !method.ReturnType.IsVar;

                case PropertyDeclarationSyntax or IndexerDeclarationSyntax or AccessorDeclarationSyntax:
                    return true;

                case BaseMethodDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     The initializer's element list, when the creation is one a collection expression reproduces.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every element has to be an ordinary expression. A <c>{ k, v }</c> pair — the dictionary
    ///     shape — is a <c>ComplexElementInitializerExpression</c> calling a two-argument <c>Add</c>,
    ///     and there is no collection-expression spelling of it; an object initializer
    ///     (<c>{ Count = 3 }</c>) is not element syntax at all. Both are ruled out here rather than by
    ///     the type check, because <c>List&lt;T&gt;</c> admits the second.
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

        if (initializer is null
            || !initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
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
    ///     ⚠ Arrays and <c>List&lt;T&gt;</c> only, and only as the declared type itself.
    /// </summary>
    /// <remarks>
    ///     These are the two targets whose collection-expression lowering is specified to produce the
    ///     same object the constructor did: a <c>T[]</c> of the same length, or a <c>List&lt;T&gt;</c>
    ///     built by the same <c>Add</c> calls. Every other target type — an interface, a builder-attributed
    ///     type, a span — is a different object with a different identity, and identity is observable.
    /// </remarks>
    static bool IsSupportedTarget(ITypeSymbol declared, INamedTypeSymbol? list) =>
        declared switch {
            IArrayTypeSymbol { IsSZArray: true } => true,
            INamedTypeSymbol named when list is not null
                => SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, list),
            _ => false
        };
}
