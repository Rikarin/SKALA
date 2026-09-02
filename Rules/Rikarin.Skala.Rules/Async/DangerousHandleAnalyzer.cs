using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3542</c> — the raw handle is taken out of its <c>SafeHandle</c> and nothing holds it open.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>SafeHandle</c> exists to make one failure impossible: the finalizer releasing a handle while
///     something is still using it, after which the operating system reuses that value and every
///     subsequent call operates on whatever now owns it. <c>DangerousGetHandle</c> returns the value
///     out from under that protection, and the reference count that would keep the handle alive for
///     the duration is <c>DangerousAddRef</c>'s job.
///     <para>
///         ⚠ <b>The call is named dangerous and is sometimes correct</b>, which is why the finding is
///         the <em>pair</em> and not the call. What is asked is the weakest question that can be
///         answered from one file with certainty: does the declaring type contain a
///         <c>DangerousAddRef</c> or <c>DangerousRelease</c> anywhere at all? Where it does, this rule
///         says nothing — whether that pair is correctly placed around this particular call is a flow
///         question, and a wrong answer to it is a false positive on code that did the difficult thing
///         right.
///     </para>
///     <para>
///         ⚠ <b>The residual cost is stated rather than guarded.</b> A type that ref-counts in a helper
///         in another file, or whose caller holds the reference, is reported and should not be. The
///         alternative — narrowing to the enclosing method — makes that worse rather than better, since
///         the documented pattern puts the <c>AddRef</c> in a <c>try</c> and the <c>Release</c> in the
///         <c>finally</c> of a wrapper. Widening to the whole type is the same decision <c>SK3530</c>
///         takes about where disposal is allowed to live, taken for the same reason.
///     </para>
///     <para>
///         ⚠ <b>Not hosted.</b> Measured outside this repository with an empty
///         <c>Directory.Build.props</c> above it, at <c>AnalysisMode=All</c>: no <c>CA</c> diagnostic of
///         any severity, hidden included, on a <c>DangerousGetHandle</c> call.
///     </para>
///     <para>
///         ⚠ <b>Fixless.</b> The repair wraps the use in a <c>DangerousAddRef</c>/<c>DangerousRelease</c>
///         pair with a <c>bool</c> success flag and a <c>finally</c> — or, more often, is to stop taking
///         the raw value and pass the <c>SafeHandle</c> itself to the P/Invoke, which the marshaller
///         already ref-counts. Which of the two applies depends on where the value goes, and no edit
///         reads that.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DangerousHandleAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DangerousHandleWithoutRefCount);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var handle = start.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle");
                if (handle is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, handle), SyntaxKind.InvocationExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol handle) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (Called(invocation) != "DangerousGetHandle"
            || invocation.ArgumentList.Arguments.Count != 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { Name: "DangerousGetHandle", Parameters.IsEmpty: true } method
            || !Derives(method.ContainingType, handle)) {
            return;
        }

        // ⚠ The withdrawal, and the whole reason this rule can be believed. A type that touches the
        // reference count anywhere is a type that knows about the reference count, and whether it
        // brackets *this* call is a flow question with no safe wrong answer.
        var declaration = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration is null || RefCounts(declaration)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + method.ContainingType.Name
                + ".DangerousGetHandle()` hands out the raw handle and `"
                + declaration.Identifier.ValueText
                + "` never calls `DangerousAddRef`, so the finalizer may release it while this value is in use"
            )
        );
    }

    static bool RefCounts(TypeDeclarationSyntax declaration) {
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            if (Called(invocation) is "DangerousAddRef" or "DangerousRelease") {
                return true;
            }
        }

        return false;
    }

    static string? Called(InvocationExpressionSyntax invocation) =>
        UsingResource.Unwrap(invocation.Expression) switch {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

    static bool Derives(ITypeSymbol? type, INamedTypeSymbol contract) {
        for (var candidate = type; candidate is not null; candidate = candidate.BaseType) {
            if (candidate.TypeKind == TypeKind.Error) {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(candidate, contract)) {
                return true;
            }
        }

        return false;
    }
}
