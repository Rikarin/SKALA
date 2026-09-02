using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2192</c> — <c>==</c> on a span answers "same memory", and it is written where "same
///     contents" was meant.
/// </summary>
/// <remarks>
///     ⚠ <b>It compiles, and the brief that carried this rule said it does not.</b> Measured on a
///     probe built outside this repository: <c>ReadOnlySpan&lt;char&gt; == ReadOnlySpan&lt;char&gt;</c>,
///     <c>Span&lt;char&gt; == Span&lt;char&gt;</c>,
///     <c>
/// ReadOnlySpan&lt;byte&gt; ==
///     ReadOnlySpan&lt;byte&gt;
///     </c> and <c>ReadOnlySpan&lt;char&gt; == string</c> all build clean at
///     <c>net10.0</c> with no compiler warning and nothing from the analyzers at
///     <c>AnalysisMode=All</c> — the span types carry their own <c>operator ==</c>. What does *not*
///     compile is <c>span.Equals(span)</c>: <c>CS1503</c>, because the only <c>Equals</c> in scope
///     takes <c>object</c> and a span cannot be boxed. So "<c>.Equals</c> where <c>SequenceEqual</c>
///     was meant" is not a shape that exists, and the operator is the whole rule.
///     <para>
///         ⚠ The <c>string</c> operand is the case that misleads hardest, because the implicit
///         conversion makes <c>span == "abc"</c> read exactly like string equality and it is a memory
///         comparison against a fresh span over the literal.
///     </para>
///     <para>
///         ⚠ <c>default</c> and <c>null</c> operands are excluded: <c>span == default</c> is the
///         idiomatic "is this the default span" test, and it is asking about memory on purpose.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpanReferenceComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SpanReferenceComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Binary);
    }

    static void Analyze(OperationAnalysisContext context) {
        var operation = (IBinaryOperation)context.Operation;
        if (operation.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
            || operation.OperatorMethod is not { ContainingType: { } declaring }
            || !IsSpan(declaring.OriginalDefinition)
            || operation.Syntax is not BinaryExpressionSyntax syntax
            || IsDefaultOrNull(syntax.Left)
            || IsDefaultOrNull(syntax.Right)
            || operation.SemanticModel is not { } model) {
            return;
        }

        var replacement = Receiver(syntax.Left) + ".SequenceEqual(" + syntax.Right + ")";

        // ⚠ Bind the replacement before offering it. `SequenceEqual` is an extension on
        // `System.MemoryExtensions`, so a file without `using System;` gets text that parses and
        // does not compile, and the element type has to satisfy whichever overload is in reach —
        // `where T : IEquatable<T>` on the two-argument one. Asking the model is the only answer
        // that stays right as the framework adds overloads; a hand-written list of element types
        // would be a guess that ages.
        var speculative = SyntaxFactory.ParseExpression(replacement);
        if (model.GetSpeculativeSymbolInfo(syntax.SpanStart, speculative, SpeculativeBindingOption.BindAsExpression)
                .Symbol is not IMethodSymbol { Name: "SequenceEqual" }) {
            return;
        }

        if (operation.OperatorKind == BinaryOperatorKind.NotEquals) {
            replacement = "!" + replacement;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                syntax.GetLocation(),
                FixEdits.Pack((syntax.Span, replacement)),
                "Spans compare equal only when they point at the same memory; "
                + "use SequenceEqual if the contents were meant"
            )
        );
    }

    static bool IsSpan(INamedTypeSymbol definition) =>
        definition.ToDisplayString() is "System.Span<T>" or "System.ReadOnlySpan<T>";

    static bool IsDefaultOrNull(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax {
            RawKind: (int)SyntaxKind.DefaultLiteralExpression or (int)SyntaxKind.NullLiteralExpression
        }
            or DefaultExpressionSyntax;

    /// <summary>
    ///     ⚠ The left operand becomes a receiver, and an operand that binds looser than member access
    ///     changes meaning when one is appended to it.
    /// </summary>
    static string Receiver(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax
            or LiteralExpressionSyntax
            ? expression.ToString()
            : "(" + expression + ")";
}
