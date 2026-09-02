using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2193</c> — <c>new ImmutableArray&lt;T&gt; { … }</c> compiles, and throws.
/// </summary>
/// <remarks>
///     The collection-initializer form calls <c>Add</c> on the value the <c>new</c> produced, and for
///     this type that value is <c>default</c> — a struct wrapping a null array. Every element
///     dereferences it, so the first one throws <c>NullReferenceException</c> at a line that reads
///     like list construction. ⚠ <b>Measured, not assumed: it builds clean.</b> A probe outside this
///     repository compiles the shape with no compiler warning and nothing from the .NET analyzers at
///     <c>AnalysisMode=All</c>, so nothing in the toolchain says a word before it runs.
///     <para>
///         ⚠ <b>An empty initializer is excluded and is a different, weaker defect.</b>
///         <c>new ImmutableArray&lt;T&gt; { }</c> calls <c>Add</c> zero times and therefore does not
///         throw; it silently produces a <c>default</c> array whose <c>IsDefault</c> is true, which
///         fails later and somewhere else. Reporting it under this rule's message — which says the
///         code throws — would be saying something untrue about it. ⚠
///         <b>
///             The parser is what excludes
///             it, and the element count below is not.
///         </b> A sabotage run found this: an empty brace pair
///         is ambiguous between the two initializer forms and Roslyn classifies it
///         <c>ObjectInitializerExpression</c>, so a <c>CollectionInitializerExpression</c> with zero
///         expressions does not exist and relaxing the count changes nothing. The count stays as a
///         statement of intent that would survive a parser change; it is not doing the work today,
///         and believing it was is what the sabotage was for.
///     </para>
///     <para>
///         ⚠ <b>The fix reuses the type's own spelling rather than assuming a <c>using</c>.</b>
///         <c>ImmutableArray&lt;int&gt;</c> becomes <c>ImmutableArray.Create&lt;int&gt;(…)</c> and
///         <c>System.Collections.Immutable.ImmutableArray&lt;int&gt;</c> keeps its qualifier, so the
///         replacement binds in exactly the files the original bound in. The type argument is written
///         out explicitly because inference from the elements is not the same answer:
///         <c>new ImmutableArray&lt;object&gt; { 1 }</c> is an array of <c>object</c> and
///         <c>ImmutableArray.Create(1)</c> is an array of <c>int</c>. The collection-expression
///         spelling <c>[…]</c> is deliberately not the fix: it needs a target type, and there is none
///         in <c>var x = new ImmutableArray&lt;int&gt; { 1 };</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmutableArrayCollectionInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.ImmutableArrayCollectionInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (creation.Initializer is not {
                RawKind: (int)SyntaxKind.CollectionInitializerExpression,
                Expressions.Count: > 0
            } initializer
            || initializer.Expressions.Any(static element => element is InitializerExpressionSyntax)
            || context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol
            is not INamedTypeSymbol { TypeKind: TypeKind.Struct } type
            || type.OriginalDefinition.ToDisplayString() != "System.Collections.Immutable.ImmutableArray<T>"
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(creation.SyntaxTree, creation.Span)) {
            return;
        }

        var (qualifier, argument) = Qualifier(creation.Type);
        if (qualifier is null || argument is null) {
            return;
        }

        var replacement = qualifier
            + ".Create<"
            + argument
            + ">("
            + string.Join(", ", initializer.Expressions.Select(static element => element.ToString()))
            + ")";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                FixEdits.Pack((creation.Span, replacement)),
                "A collection initializer calls Add on the default ImmutableArray, which throws; "
                + "build it with ImmutableArray.Create"
            )
        );
    }

    /// <summary>
    ///     The type name without its argument list, spelled exactly as the source spells it, and the
    ///     element type beside it.
    /// </summary>
    static (string? Qualifier, TypeSyntax? Argument) Qualifier(TypeSyntax type) =>
        type switch {
            GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic =>
                (generic.Identifier.ValueText, generic.TypeArgumentList.Arguments[0]),
            QualifiedNameSyntax { Right: GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } right } qualified =>
                (qualified.Left + "." + right.Identifier.ValueText, right.TypeArgumentList.Arguments[0]),
            AliasQualifiedNameSyntax {
                Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } name
            } alias => (alias.Alias + "::" + name.Identifier.ValueText, name.TypeArgumentList.Arguments[0]),
            _ => (null, null)
        };
}
