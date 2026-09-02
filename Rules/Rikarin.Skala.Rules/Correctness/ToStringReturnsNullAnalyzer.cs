using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2110</c> — an override of <c>object.ToString()</c> that returns a null constant.
/// </summary>
/// <remarks>
///     ⚠ <b>The whole rule is the boundary against <c>CS8603</c>, and the boundary was measured.</b> A
///     probe built with the .NET 10.0.400 SDK reports <c>CS8603</c> on
///     <c>public override string ToString() { return null; }</c> in a nullable-enabled context, so that
///     shape is declined: ADR-008 says a diagnostic the platform already ships is used, not rebuilt.
///     The same probe found the compiler silent on the two shapes this rule reports.
///     <list type="bullet">
///         <item>
///             <c>public override string? ToString() => null;</c> — legal and warning-free, because
///             <c>object.ToString()</c> is itself annotated as returning <c>string?</c>. The annotation is
///             there for the runtime's own edge cases and every caller ignores it.
///         </item>
///         <item>
///             The same <c>return null;</c> under <c>#nullable disable</c>, where no nullable diagnostic
///             can be issued at all.
///         </item>
///     </list>
///     <para>
///         ⚠ <b>This rule does not withdraw when the nullable context is disabled — it is one of the two
///         places it is for.</b> That inverts the direction the rest of this batch runs in, and it is
///         safe to invert here because the question asked is <c>GetConstantValue</c> rather than a flow
///         state: a <c>null</c> literal is null in every nullable context, and the compiler's flow
///         analysis is not consulted.
///     </para>
///     <para>
///         ⚠ Constants only. A <c>ToString()</c> returning a variable that <em>might</em> be null is a
///         flow question, and in the two contexts this rule covers there is no flow state to read.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToStringReturnsNullAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TostringCanReturnNull);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.Identifier.ValueText != "ToString" || declaration.ParameterList.Parameters.Count != 0) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not
            { IsOverride: true } method) {
            return;
        }

        if (!OverridesObjectToString(method)) {
            return;
        }

        // ⚠ The two conditions are a disjunction, not a conjunction. Either the compiler cannot issue a
        // nullable warning here at all, or it can and still says nothing because the method declares
        // `string?` — the annotation `object.ToString()` itself carries. Where neither holds, CS8603 is
        // the finding and this rule is silent.
        var declaredNullable = method.ReturnNullableAnnotation == NullableAnnotation.Annotated;

        foreach (var expression in ReturnedExpressions(declaration)) {
            if (!NullabilityFacts.IsConstantNull(context.SemanticModel, expression, context.CancellationToken)) {
                continue;
            }

            if (!declaredNullable
                && NullabilityFacts.WarningsEnabledAt(context.SemanticModel, expression.SpanStart)) {
                continue;
            }

            if (RewriteGuards.ContainsCommentOrDirective(expression)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    expression.GetLocation(),
                    FixEdits.Pack((expression.Span, "string.Empty")),
                    "`" + method.ContainingType.Name + ".ToString()` returns `" + expression + "`"
                )
            );
        }
    }

    /// <summary>
    ///     ⚠ Walk the override chain rather than trusting the name.
    /// </summary>
    /// <remarks>
    ///     A parameterless <c>ToString</c> that overrides something else exists — a base class may have
    ///     introduced its own <c>virtual string ToString()</c> hiding <c>object</c>'s, and its contract
    ///     is that base class's to state. Only the chain that ends at <see cref="object" /> is the
    ///     contract every interpolation and every logger relies on.
    /// </remarks>
    static bool OverridesObjectToString(IMethodSymbol method) {
        for (var current = method; current is not null; current = current.OverriddenMethod) {
            if (current.ContainingType?.SpecialType == SpecialType.System_Object) {
                return true;
            }
        }

        return false;
    }

    static IEnumerable<ExpressionSyntax> ReturnedExpressions(MethodDeclarationSyntax declaration) {
        if (declaration.ExpressionBody?.Expression is { } arrow) {
            yield return arrow;
            yield break;
        }

        if (declaration.Body is not { } body) {
            yield break;
        }

        foreach (var node in NullabilityFacts.DescendantsWithinTheSameFunction(body)) {
            if (node is ReturnStatementSyntax { Expression: { } returned }) {
                yield return returned;
            }
        }
    }
}
