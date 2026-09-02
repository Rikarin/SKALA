using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The <c>GetService&lt;T&gt;()!</c> shape, shared by <c>SK2113</c> which reports it and
///     <c>SK2111</c> which declines it.
/// </summary>
/// <remarks>
///     ⚠ The two rules are disjoint <b>by construction rather than by filter</b>: this predicate is the
///     single definition of the shape, <c>SK2113</c> fires exactly when it holds and <c>SK2111</c>
///     exactly when it does not, so no expression can produce both findings and neither rule can drift
///     into the other's ground.
/// </remarks>
internal static class ServiceResolution {
    const string Extensions = "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";

    /// <summary>
    ///     The suppressed resolution call, or <c>null</c> when this is some other <c>!</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The generic extension only, and the reason is the fix rather than the finding.</b>
    ///     <c>provider.GetService(typeof(T))!</c> is the same mistake, but its counterpart —
    ///     <c>GetRequiredService(this IServiceProvider, Type)</c> — is an extension method in a
    ///     namespace the file need not have imported, since the interface call compiles without it. The
    ///     generic form cannot have that problem: it is *already* a call into
    ///     <c>ServiceProviderServiceExtensions</c>, so renaming it lands on a sibling of a method that
    ///     has already resolved.
    /// </remarks>
    public static (IMethodSymbol Method, SimpleNameSyntax Name)? Match(
        SemanticModel model,
        PostfixUnaryExpressionSyntax suppression,
        CancellationToken token
    ) {
        var operand = suppression.Operand;
        while (operand is ParenthesizedExpressionSyntax parenthesized) {
            operand = parenthesized.Expression;
        }

        if (operand is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method) {
            return null;
        }

        if (Replacement(method.Name) is null || method.TypeArguments.Length != 1) {
            return null;
        }

        var container = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (container != "global::" + Extensions) {
            return null;
        }

        var name = invocation.Expression switch {
            MemberAccessExpressionSyntax access => access.Name,
            MemberBindingExpressionSyntax binding => binding.Name,
            SimpleNameSyntax simple => simple,
            _ => null
        };

        return name is null ? null : (method, name);
    }

    /// <summary>The throwing counterpart, or <c>null</c> for a name that has none.</summary>
    public static string? Replacement(string name) =>
        name switch {
            "GetService" => "GetRequiredService",
            "GetKeyedService" => "GetRequiredKeyedService",
            _ => null
        };
}

/// <summary>
///     <c>SK2113</c> — <c>provider.GetService&lt;T&gt;()!</c> where <c>GetRequiredService&lt;T&gt;()</c>
///     was meant.
/// </summary>
/// <remarks>
///     <c>GetService&lt;T&gt;()</c> answers <c>null</c> for a service nobody registered. The <c>!</c> is
///     the author asserting somebody did, and when the assertion is wrong the program does not fail at
///     the resolution — it fails at the first use, with a <c>NullReferenceException</c> naming neither
///     the service type nor the container. <c>GetRequiredService&lt;T&gt;()</c> is the same call with
///     the assertion moved into the framework, throwing at the line that asked.
///     <para>
///         ⚠ <b>The nullable context does not silence this rule and it is not an oversight.</b> The
///         question asked is the <c>!</c> token and the callee's containing type; no flow state is read,
///         so the rule reports the identical mistake under <c>#nullable disable</c> — which is where the
///         compiler says least and the `!` is most likely to have been copied in without thought.
///     </para>
///     <para>
///         ⚠ The <c>!</c> here is <b>not</b> redundant, which is exactly why <c>SK2111</c> declines it.
///         It suppresses a warning that is telling the truth; the fix is to stop needing it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivenServiceResolutionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullForgivenServiceResolution);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SuppressNullableWarningExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var suppression = (PostfixUnaryExpressionSyntax)context.Node;
        var match = ServiceResolution.Match(context.SemanticModel, suppression, context.CancellationToken);
        if (match is null) {
            return;
        }

        var (method, name) = match.Value;
        var replacement = ServiceResolution.Replacement(method.Name);
        if (replacement is null) {
            return;
        }

        // Two edits: the rename, and the `!` that the rename makes pointless. They are one fix because
        // applying either alone leaves code that is worse than what was there — a `GetRequiredService`
        // still carrying a suppression, or a `GetService` whose warning is now unsuppressed.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                suppression.GetLocation(),
                FixEdits.Pack(
                    (name.Identifier.Span, replacement),
                    (suppression.OperatorToken.Span, string.Empty)
                ),
                "`" + method.Name + "<" + method.TypeArguments[0].Name + ">()!` should be `" + replacement + "`"
            )
        );
    }
}
