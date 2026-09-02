using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2041</c> — an equality member delegates to <c>object</c>'s identity implementation.</summary>
/// <remarks>
///     ⚠ The receiver is matched on the <c>base</c> keyword and the target on where the call
///     <em>binds</em>. A base class that overrides equality is doing real work and delegating to it
///     is correct; a struct's <c>base.GetHashCode()</c> binds to <c>ValueType.GetHashCode</c>, which
///     does hash the fields. Neither is a finding, and neither needed an exclusion.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BaseEqualityCallAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.BaseEqualityCallIsIdentity);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } access) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (name is not ("Equals" or "GetHashCode")
            || !EqualityMembers.InsideAnEqualityMember(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.ContainingType?.SpecialType != SpecialType.System_Object) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                name == "GetHashCode"
                    ? "`base.GetHashCode()` is object's identity hash, so equal instances hash differently"
                    : "`base.Equals` is object's reference equality, so the override answers the question it overrides"
            )
        );
    }
}
