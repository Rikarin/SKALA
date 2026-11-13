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
///         <c>RedundantEmptyObjectOrCollectionInitializer</c> is the shape: <c>new Foo { }</c> is
///         <c>new Foo()</c> with two characters of ceremony.
///     </para>
///     <para>
///         ⚠ <b>An empty <c>with</c> is not reported, on anything (#636).</b> This rule used to read
///         <c>x with { }</c> as a copy that changes nothing and fix it to <c>x</c>, and that is a different
///         program: <c>with</c> on a record class calls the copy constructor and returns a <em>distinct</em>
///         instance, so the fix aliases a clone the author made in order to keep it apart — in Vixen it made
///         an extracted texture layer share its instance with the stack it came from, and turned two
///         equality tests into comparisons of an object with itself. A record's copy constructor can be
///         written by hand and do anything. On a struct, a record struct or an anonymous type the copy is
///         a value, and it is still not <c>x</c>: <c>(s with { }).Mutate()</c> mutates a temporary where
///         <c>s.Mutate()</c> mutates <c>s</c>, and an anonymous instance is a reference. The empty
///         <c>with</c> is the one way C# spells "a copy of this", so it is never redundant.
///     </para>
///     <para>
///         ⚠ The fix stays <c>fixIsSafe: false</c> without the <c>with</c>, because <c>new Foo { }</c> and
///         <c>new Foo()</c> are not the same <em>expression tree</em>: inside a lambda converted to
///         <c>Expression&lt;…&gt;</c> the first is a <c>MemberInitExpression</c> with no bindings and the
///         second a <c>NewExpression</c>, and a query provider may translate the two differently. Compiled
///         code cannot tell them apart.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptyInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(
            AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression
        );
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
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(node.SyntaxTree, span)) {
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
