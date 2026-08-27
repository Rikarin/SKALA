using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>
/// <c>SK8005</c> — <c>Thread.Sleep</c> inside a test method.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK8000 — Tests". A sleep encodes a guess about how long the
/// thing under test takes. The guess holds on an idle laptop and fails on a CI agent running
/// sixteen jobs, and because it fails intermittently it is triaged as flakiness rather than as a
/// defect — which is how a suite stops being read.
/// <para>
/// ⚠ There is no fix, and <c>hasFix: false</c> in the catalogue says so. The replacement is a
/// change to what the test synchronises on — a handle, a task, a polled predicate with a generous
/// timeout — and that is a design decision rather than an edit. docs/plan/10: a fixing tool that
/// guesses is a tool an agent will use to break the build.
/// </para>
/// <para>
/// ⚠ Scoped by attribute rather than by path, which is the same choice
/// <see cref="AsyncContext.IsTestMethod"/> already made for <c>SK3002</c> and for the same reason:
/// a rule staying silent has to be right in a repository whose tests do not live under a
/// <c>*.Tests</c> folder. The category-wide instrument is still the <c>.editorconfig</c> section
/// doc 08 describes, and this rule's <c>suggestion</c> default is what a repository promotes there.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThreadSleepInTestAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ThreadSleepInTest);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var thread = start.Compilation.GetTypeByMetadataName("System.Threading.Thread");
                if (thread is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, thread),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol thread) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // The cheap half first: a name test over syntax, so the semantic model is asked about the
        // handful of calls that could possibly be this rather than about every invocation.
        if (invocation.Expression is not (MemberAccessExpressionSyntax {
                Name.Identifier.ValueText: "Sleep"
            } or IdentifierNameSyntax { Identifier.ValueText: "Sleep" })) {
            return;
        }

        if (!AsyncContext.IsTestMethod(invocation)) {
            return;
        }

        // ⚠ `System.Threading.Thread.Sleep` as the model resolves it, and nothing else. A `Sleep`
        // on a user type of that name is a different method, and a call that did not resolve is a
        // question the rule cannot answer — docs/plan/07 § loose: silence, not a guess.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { IsStatic: true, Name: "Sleep" } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, thread)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                Describe(invocation)
            )
        );
    }

    /// <summary>
    /// ⚠ The message names the duration when it is written as a literal, because that is the number
    /// the reader is deciding about. A sleep of 5 ms and a sleep of 5 s are the same finding and
    /// very different conversations.
    /// </summary>
    static string Describe(InvocationExpressionSyntax invocation) {
        const string Advice = "; a test that waits for a duration passes or fails on how loaded the machine is";

        if (invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax {
                RawKind: (int)SyntaxKind.NumericLiteralExpression
            } literal) {
            return "`Thread.Sleep(" + literal.Token.ValueText + ")` in a test" + Advice;
        }

        return "`Thread.Sleep` in a test" + Advice;
    }
}
