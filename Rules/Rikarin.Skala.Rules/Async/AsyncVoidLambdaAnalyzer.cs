using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3052</c> — an <c>async</c> lambda converted to a delegate that returns <c>void</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000". ⚠
///     <b>
///         This is <c>async void</c> in a disguise no
///         signature carries.
///     </b> A lambda has no written return type: it takes the one the target delegate
///     asks for, so <c>Register(async () =&gt; await SaveAsync())</c> against a <c>Register(Action)</c>
///     compiles into an <c>async void</c> with every one of its consequences. <c>Register</c> returns
///     the instant the lambda reaches its first <c>await</c>, the rest of the body runs unobserved, and
///     an exception in it is handed to the synchronization context rather than to anyone who could
///     catch it. Nothing in the source says <c>void</c>, which is why reading the code does not find it.
///     <para>
///         ⚠ <b><c>SK3001</c> cannot see this and neither can <c>SK3005</c>.</b> <c>SK3001</c> matches
///         a <c>MethodDeclarationSyntax</c> whose return type is written <c>void</c>; there is no such
///         token here. <c>SK3005</c> matches a task-producing call discarded as a statement in a
///         <em>synchronous</em> body, and this body is <c>async</c> — so it returns before it looks.
///         The two are disjoint by construction rather than by <c>supersedes</c>, and
///         <c>AsyncVoidShapeBatchTests</c> pins it on a fixture that satisfies both shapes at once.
///     </para>
///     <para>
///         ⚠
///         <b>
///             An event-handler delegate is excluded, and it is the same exclusion <c>SK3001</c>
///             makes.
///         </b> <c>(object, TEventArgs) -&gt; void</c> is the shape the language gives events, an
///         <c>async</c> handler subscribed to one is the sanctioned use of <c>async void</c>, and there
///         is no other signature available to it. What is left is the case the rule is about: a
///         <c>void</c> delegate somebody chose, where a <c>Func&lt;Task&gt;</c> could have been asked
///         for instead. The throw inside such a handler is still reported — by <c>SK3050</c>, which is
///         where the remedy for it lives.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidLambdaAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncVoidLambda);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var eventArgs = start.Compilation.GetTypeByMetadataName("System.EventArgs");

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, eventArgs),
                    SyntaxKind.SimpleLambdaExpression,
                    SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.AnonymousMethodExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? eventArgs) {
        var lambda = (AnonymousFunctionExpressionSyntax)context.Node;
        if (lambda.AsyncKeyword.RawKind == (int)SyntaxKind.None) {
            return;
        }

        // ⚠ The *converted* type, never the type of the lambda itself. A lambda has no type of its own
        // until a conversion gives it one, and that conversion is the whole defect: the same text is
        // correct against `Func<Task>` and a process-killer against `Action`.
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).ConvertedType
            is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } target
            || target.DelegateInvokeMethod is not { ReturnsVoid: true } invoke
            || AsyncSignature.HasEventHandlerShape(invoke, eventArgs)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                lambda.AsyncKeyword.GetLocation(),
                "this `async` lambda becomes `"
                + target.Name
                + "`, which returns `void`, so nothing waits for it and its exceptions cannot be caught"
            )
        );
    }
}
