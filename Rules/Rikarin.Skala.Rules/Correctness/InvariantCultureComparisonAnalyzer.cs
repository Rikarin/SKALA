using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2151</c> — an equality-shaped string operation given <c>StringComparison.InvariantCulture</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Culture, comparison policy and query shape".
///     <para>
///         ⚠ <b>The mirror image of <c>SK2010</c>, and the harder half to see.</b> <c>SK2010</c> reports
///         a policy that is <em>missing</em>, which reads as an oversight. This reports one that is
///         <em>stated</em>, which reads as a decision and therefore survives review — the author typed
///         an explicit <c>StringComparison</c>, so the line looks like the fixed version of itself.
///     </para>
///     <para>
///         ⚠ <b>Invariant is culture-<em>stable</em>, not culture-<em>free</em>.</b> It still walks the
///         collation tables, so it is an order of magnitude slower than <c>Ordinal</c> and it still
///         answers <c>true</c> for sequences that are not the same string — a zero-width joiner
///         compares equal to nothing at all.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <see cref="System.Globalization.CultureInfo" />.InvariantCulture is never reported, and
///             the exclusion is structural.
///         </b> Invariant culture is <em>correct</em> for round-tripping
///         formatted data, and a rule that could not tell a comparison from a <c>ToString</c> would be
///         advising authors to corrupt their own serialisation. This rule keys on the
///         <c>System.StringComparison</c> enum, which no formatting or parsing API accepts, so a
///         <c>CultureInfo</c> argument cannot reach it by any path.
///     </para>
///     <para>
///         ⚠ <b>Ordering is excluded on purpose.</b> <c>Compare</c> and <c>CompareTo</c> are silent: a
///         comparison whose result feeds a sort is the one place linguistic collation is legitimately
///         wanted, and a sort that must not be linguistic is a design decision rather than a slip.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvariantCultureComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InvariantCultureComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (start.Compilation.GetTypeByMetadataName("System.StringComparison") is not { } comparison) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, comparison),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol comparison) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList is not { } arguments
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String) {
            return;
        }

        // ⚠ Equality-shaped members only. `Compare`/`CompareTo` order, and ordering is where
        // linguistic collation legitimately lives.
        if (method.Name is not ("Equals" or "StartsWith" or "EndsWith" or "Contains" or "IndexOf" or "LastIndexOf")) {
            return;
        }

        foreach (var argument in arguments.Arguments) {
            // ⚠ Read the *expression's* type rather than trusting the syntax: a `using static
            // System.StringComparison;` or an alias makes `InvariantCulture` a bare identifier, and a
            // constant of another enum can share the member name.
            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type,
                    comparison
                )) {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol
                is not IFieldSymbol { IsConst: true } member) {
                continue;
            }

            var replacement = member.Name switch {
                "InvariantCulture" => "Ordinal",
                "InvariantCultureIgnoreCase" => "OrdinalIgnoreCase",
                _ => null
            };

            if (replacement is null) {
                continue;
            }

            // The edit replaces only the member name, so it inherits whatever qualification the
            // author wrote — `StringComparison.X`, `System.StringComparison.X` or a bare `X` under a
            // `using static`. Nothing here has to know which.
            var name = argument.Expression is MemberAccessExpressionSyntax access
                ? (SyntaxNode)access.Name
                : argument.Expression;

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    argument.Expression.GetLocation(),
                    FixEdits.Pack((name.Span, replacement)),
                    "`"
                    + method.Name
                    + "` is asked for StringComparison."
                    + member.Name
                    + ", which still runs the full linguistic comparison; an equality test on an "
                    + "identifier, key or path wants StringComparison."
                    + replacement
                )
            );
        }
    }
}
