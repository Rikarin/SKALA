using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2232</c> — an <c>AssemblyLoadContext.Load</c> override that loads into some other context.
/// </summary>
/// <remarks>
///     <para>
///         The override exists to place an assembly in <em>this</em> context.
///         <c>Assembly.LoadFrom</c> places it in the default one and <c>Assembly.LoadFile</c> places
///         it in a brand new anonymous one, so in both cases the context whose resolver was asked
///         ends up not holding what it resolved. The symptom is the one the isolation was bought to
///         prevent: the assembly is present twice, and a type from one copy will not cast to the
///         identically-named type from the other.
///     </para>
///     <para>
///         ⚠ <b>This is the sound core of `S3885`, and the broad reading of that rule is deliberately
///         not implemented.</b> "`Assembly.Load` should be used" reported everywhere would report
///         every plugin host in existence — <c>LoadFrom</c> against a path is exactly right when the
///         default context is where the assembly belongs, and which context an assembly belongs in is
///         intent rather than a fact in the file. Inside a <c>Load</c> override the intent <em>is</em>
///         stated, by the override existing, and this is the one position where the question has an
///         answer.
///     </para>
///     <para>
///         ⚠ <b><c>Assembly.Load</c> inside the override is not reported</b>, and that exclusion is
///         the point of the rule rather than a concession to it: returning <c>Assembly.Load(name)</c>
///         is the documented way to say "this dependency is shared — take it from the default
///         context", which is how a plugin and its host agree on a contract assembly.
///     </para>
///     <para>
///         ⚠ <b>Nothing else reports it.</b> Probed outside this repository on SDK 10.0.400 at
///         <c>AnalysisMode=All</c>: <c>Assembly.LoadFrom</c> and <c>Assembly.LoadFile</c> draw
///         <c>IL2026</c> only when the trim analyzer is switched on, and that diagnostic is about
///         trimming rather than about which context the result lands in. At plain defaults, and in
///         every configuration without the trim analyzer, they produce nothing at all.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AssemblyLoadedOutsideItsContextAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AssemblyLoadedOutsideItsContext);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (start.Compilation.GetTypeByMetadataName("System.Runtime.Loader.AssemblyLoadContext") is not { }
                    loadContext) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, loadContext),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol loadContext) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ The single-argument path overloads only. `LoadFrom` also takes a hash and a hash
        // algorithm; matching by argument count makes a future overload a miss rather than a wrong
        // fix, because `LoadFromAssemblyPath` has nowhere to put the extra arguments.
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Name.Identifier.ValueText is not ("LoadFrom" or "LoadFile")) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                IsStatic: true,
                Parameters.Length: 1,
                ContainingType: { } containing
            }
            || containing.ToDisplayString() != "System.Reflection.Assembly") {
            return;
        }

        if (!SitsInsideTheContextsOwnResolver(invocation, model, loadContext, cancellation)) {
            return;
        }

        // ⚠ The edit replaces the `Assembly.LoadFrom` member access with the inherited instance
        // method's name and leaves the argument alone, so the path expression is not moved.
        var properties = RewriteGuards.ContainsCommentOrDirective(invocation.SyntaxTree, access.Span)
            ? null
            : FixEdits.Pack((access.Span, "LoadFromAssemblyPath"));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                access.Name.GetLocation(),
                properties,
                "`Assembly."
                + access.Name.Identifier.ValueText
                + "` loads into "
                + (access.Name.Identifier.ValueText == "LoadFrom"
                    ? "the default context"
                    : "a new anonymous context")
                + ", not into this one — use `LoadFromAssemblyPath`"
            )
        );
    }

    /// <summary>
    ///     Whether the call sits directly in an override of <c>AssemblyLoadContext.Load</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ A lambda or a local function between the call and the override withdraws the finding. A
    ///     <c>static</c> lambda has no <c>this</c> to call the instance method on, so the fix would
    ///     not compile there — and the walk stops at the first one rather than trying to decide which
    ///     kinds are safe.
    /// </remarks>
    static bool SitsInsideTheContextsOwnResolver(
        SyntaxNode node,
        SemanticModel model,
        INamedTypeSymbol loadContext,
        System.Threading.CancellationToken cancellation
    ) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    return false;

                case MethodDeclarationSyntax declaration:
                    return model.GetDeclaredSymbol(declaration, cancellation) is { IsOverride: true } method
                        && OverridesLoad(method, loadContext);

                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    static bool OverridesLoad(IMethodSymbol method, INamedTypeSymbol loadContext) {
        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod) {
            if (string.Equals(current.Name, "Load", StringComparison.Ordinal)
                && SymbolEqualityComparer.Default.Equals(current.ContainingType, loadContext)) {
                return true;
            }
        }

        return false;
    }
}
