using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0230</c> — a pair of braces that sets nothing.
/// </summary>
/// <remarks>
///     <para>
///         Three ReSharper inspections are one shape: <c>RedundantEmptyObjectOrCollectionInitializer</c>,
///         <c>RedundantWithExpression</c> and <c>redundant_empty_with_element</c>. <c>new Foo { }</c> is
///         <c>new Foo()</c> with two characters of ceremony, and <c>x with { }</c> is a record copy that
///         changes nothing.
///     </para>
///     <para>
///         ⚠ <b>The fix is not safe, and the empty <c>with</c> is why.</b> <c>new Foo { }</c> →
///         <c>new Foo()</c> is a pure deletion with no observable effect at all. <c>x with { }</c> → <c>x</c>
///         is not: <c>with</c> invokes the record's copy constructor, so it allocates a <em>distinct</em>
///         instance and it dereferences <c>x</c>. Removing it aliases a clone the author may have written
///         deliberately in order to mutate it, and it removes a <c>NullReferenceException</c> that a null
///         operand would have thrown. Both are the sort of change a person has to look at, so the rule
///         carries <c>fixIsSafe: false</c> rather than splitting into two safety classes for one concept.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptyInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression
        );
        context.RegisterSyntaxNodeAction(AnalyzeWith, SyntaxKind.WithExpression);
    }

    static void AnalyzeCreation(SyntaxNodeAnalysisContext context) {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsEmpty(creation.Initializer)) {
            return;
        }

        // ⚠ Only the two initializer kinds that mean "and then set these members". A
        // `ComplexElementInitializerExpression` never appears here, and an array initializer belongs
        // to an ArrayCreationExpression, which is a different node this action never sees — removing
        // `{ }` from `new int[] { }` would leave text that does not compile.
        if (!creation.Initializer!.IsKind(SyntaxKind.ObjectInitializerExpression)
            && !creation.Initializer.IsKind(SyntaxKind.CollectionInitializerExpression)) {
            return;
        }

        // The argument list is what the initializer collapses into, so a creation with no argument
        // list has to grow one: `new Foo { }` is `new Foo()`, not `new Foo`.
        var start = creation.ArgumentList is { } arguments
            ? arguments.Span.End
            : creation is ObjectCreationExpressionSyntax { Type: { } type }
                ? type.Span.End
                : -1;
        if (start < 0) {
            return;
        }

        var replacement = creation.ArgumentList is null ? "()" : string.Empty;
        Report(
            context,
            creation,
            TextSpan.FromBounds(start, creation.Span.End),
            replacement,
            "The object initializer is empty"
        );
    }

    static void AnalyzeWith(SyntaxNodeAnalysisContext context) {
        var with = (WithExpressionSyntax)context.Node;
        if (!IsEmpty(with.Initializer)) {
            return;
        }

        Report(
            context,
            with,
            TextSpan.FromBounds(with.Expression.Span.End, with.Span.End),
            string.Empty,
            "The `with` expression sets nothing, so it copies for nothing"
        );
    }

    static bool IsEmpty(InitializerExpressionSyntax? initializer) => initializer is { Expressions.Count: 0 };

    static void Report(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax node,
        TextSpan span,
        string replacement,
        string message
    ) {
        // The braces are deleted wholesale, so anything a person wrote between them would go with
        // them. An empty initializer holding a comment is a note about why it is empty.
        if (RewriteGuards.ContainsCommentOrDirective(node.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );
    }
}
