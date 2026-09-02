using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2143</c> — two arguments handed crosswise to the two parameters they are named after.
/// </summary>
/// <remarks>
///     <para>
///         Two same-typed parameters side by side are the one place a transposed call compiles, runs,
///         and passes any test that puts both values through symmetric code. The only evidence
///         available is the naming: when the variable called <c>source</c> is sitting in
///         <c>destination</c> and the one called <c>destination</c> is sitting in <c>source</c>, the
///         author's own names say which way round they meant it and the call says the other.
///     </para>
///     <para>
///         ⚠ <b><c>Copy(source, destination)</c> called as <c>Copy(destination, source)</c> is
///         undetectable in general and this rule does not try.</b> The sound signal is the crosswise
///         name match and nothing looser: adjacent parameters, identical types, plain identifiers, and
///         both names at least three characters. <c>Max(y, x)</c> and <c>Add(b, a)</c> are where
///         reversal is deliberate, and a one-letter name is no evidence about intent at all.
///     </para>
///     <para>
///         ⚠ <b>A call to a method of the enclosing member's own name is declined</b>, which is the
///         guard for the deliberate reversal that is genuinely correct: a descending comparer written
///         as <c>Compare(right, left)</c> delegating to another <c>Compare</c> is exactly this shape
///         and exactly not a defect.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrosswiseArgumentOrderAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     ⚠ Below this, a name carries no evidence. <c>a</c>/<c>b</c>, <c>x</c>/<c>y</c> and
    ///     <c>lo</c>/<c>hi</c> are the vocabulary of code that reverses its arguments on purpose.
    /// </summary>
    const int ShortestTellingName = 3;

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CrosswiseArgumentOrder);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        Analyze(context, invocation, invocation.ArgumentList);
    }

    static void AnalyzeCreation(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is { } list) {
            Analyze(context, creation, list);
        }
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ExpressionSyntax call, ArgumentListSyntax list) {
        var arguments = list.Arguments;
        if (arguments.Count < 2
            || context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        // ⚠ Positional only, no byref, no `params`, and never more arguments than parameters. Each
        // makes "the parameter this argument fills" an inference rather than a fact — the assumption
        // #298 found SK0232 making, and the reason its loop indexed off the end of the parameter
        // array. Fewer arguments than parameters is fine: omitted optionals come off the end, so
        // position still is the parameter's index for every argument that was written.
        //
        // ⚠ This guard and the `params` filter below cover each other, and neither can be sabotaged
        // alone — which is worth writing down rather than claiming each is load-bearing. Valid C#
        // cannot supply more arguments than there are parameters *without* a `params` parameter, so
        // the filter already makes the over-supply branch unreachable and this comparison is the
        // invariant made explicit. What is pinned, and what #298 actually teaches, is the loop bound
        // below: with the index bounded on the array it reads, removing both guards produces a wrong
        // finding that a fixture catches instead of a crash that makes every fixture pass.
        if (arguments.Count > method.Parameters.Length) {
            return;
        }

        foreach (var parameter in method.Parameters) {
            if (parameter.RefKind != RefKind.None || parameter.IsParams) {
                return;
            }
        }

        foreach (var argument in arguments) {
            if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
                return;
            }
        }

        if (DelegatesToItsOwnName(call, method)) {
            return;
        }

        // ⚠ Bounded on both arrays rather than on the one that happens to be shorter in the
        // well-formed case. This is #298's lesson applied instead of quoted: SK0232 bounds its
        // counter on the parameter count and indexes with the argument position, and those are the
        // same number only when the call is exactly arity-matched.
        var pairs = arguments.Count < method.Parameters.Length ? arguments.Count : method.Parameters.Length;

        for (var i = 0; i + 1 < pairs; i++) {
            if (!Crosswise(method.Parameters[i], method.Parameters[i + 1], arguments[i], arguments[i + 1])) {
                continue;
            }

            var left = arguments[i].Expression.Span;
            var right = arguments[i + 1].Expression.Span;
            if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, TextSpan.FromBounds(
                    left.Start,
                    right.End
                ))) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    Location.Create(context.Node.SyntaxTree, TextSpan.FromBounds(left.Start, right.End)),
                    FixEdits.Pack(
                        (left, arguments[i + 1].Expression.ToString()),
                        (right, arguments[i].Expression.ToString())
                    ),
                    "'"
                    + arguments[i].Expression
                    + "' is passed as '"
                    + method.Parameters[i].Name
                    + "' and '"
                    + arguments[i + 1].Expression
                    + "' as '"
                    + method.Parameters[i + 1].Name
                    + "'"
                )
            );

            // One pair per call. A second overlapping pair would produce two fixes editing the same
            // argument, and `skala fix` applies them independently.
            return;
        }
    }

    static bool Crosswise(
        IParameterSymbol first,
        IParameterSymbol second,
        ArgumentSyntax left,
        ArgumentSyntax right
    ) {
        // ⚠ Type parameters are excluded: two `T` arguments to a generic helper are the same shape
        // with none of the evidence, and the reversal there is usually the point.
        if (first.Type.TypeKind == TypeKind.TypeParameter
            || !SymbolEqualityComparer.Default.Equals(first.Type, second.Type)) {
            return false;
        }

        if (first.Name.Length < ShortestTellingName || second.Name.Length < ShortestTellingName) {
            return false;
        }

        return left.Expression is IdentifierNameSyntax leftName
            && right.Expression is IdentifierNameSyntax rightName
            && Same(leftName.Identifier.Text, second.Name)
            && Same(rightName.Identifier.Text, first.Name);
    }

    /// <summary>
    ///     ⚠ Case-insensitive, because a parameter <c>source</c> filled by a field <c>Source</c> is the
    ///     same evidence and C#'s own conventions guarantee the case will differ.
    /// </summary>
    static bool Same(string argument, string parameter) =>
        string.Equals(argument, parameter, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether this call delegates to a method of the enclosing member's own name.
    /// </summary>
    /// <remarks>
    ///     ⚠ The guard for the reversal that is deliberate and correct. A descending comparer's
    ///     <c>Compare(T left, T right) =&gt; inner.Compare(right, left)</c> is the crosswise shape
    ///     exactly, and swapping it back is the one edit that would break it.
    /// </remarks>
    static bool DelegatesToItsOwnName(SyntaxNode node, IMethodSymbol method) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax declaration:
                    return string.Equals(declaration.Identifier.Text, method.Name, StringComparison.Ordinal);

                case LocalFunctionStatementSyntax local:
                    return string.Equals(local.Identifier.Text, method.Name, StringComparison.Ordinal);

                case BaseTypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
