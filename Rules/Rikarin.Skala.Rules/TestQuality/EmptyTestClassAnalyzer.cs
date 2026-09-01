using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>
///     <c>SK8021</c> — a class that declares itself a test class and holds no test.
/// </summary>
/// <remarks>
///     ⚠ It is the same silence <c>SK8020</c> reports, arrived at from the other side: the runner opens the
///     type, finds nothing to run, and says nothing about it. A file that once held tests and lost them to
///     a refactor looks exactly like a file that passes.
///     <para>
///         ⚠ <b>Decidable from attributes alone, which is what separates it from the cut <c>SK8001</c>.</b>
///         That rule had to decide whether a method asserts anything, and an assertion inside a helper is
///         indistinguishable from no assertion without following the call. This one asks whether any method
///         on the type or on any of its base types carries a test attribute — a question about attributes,
///         answered without following anything.
///     </para>
///     <para>
///         ⚠ MSTest and NUnit only. xUnit has no class-level attribute, so there is no declaration of
///         intent to contradict; a rule covering it would have to decide from the class's *name* that it
///         was meant to hold tests, which is a convention and not a fact.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyTestClassAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TestClassWithoutTests);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var frameworks = TestFrameworks.Resolve(start.Compilation);
                if (frameworks.MsTestClassAttribute is null && frameworks.NUnitFixtureAttribute is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, frameworks),
                    SyntaxKind.ClassDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, TestFrameworks frameworks) {
        var declaration = (ClassDeclarationSyntax)context.Node;

        // ⚠ An abstract class carrying the attribute is the shared-base pattern both frameworks
        // support: it holds the hooks and the derived fixtures hold the tests, so it is meant to be
        // empty of them.
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
            || !TestFrameworks.IsAnchorDeclaration(type, declaration)) {
            return;
        }

        var marker = Declares(type, frameworks);
        if (marker is null) {
            return;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers()) {
                if (TestFrameworks.Carries(member, frameworks.TestMethodAttributes)
                    || TestFrameworks.Carries(member, frameworks.LifecycleAttributes)) {
                    return;
                }
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + type.Name
                + "` is marked `["
                + marker
                + "]` and declares no test, so the runner reports nothing about it"
            )
        );
    }

    /// <summary>
    ///     The framework whose class attribute the type carries, or <c>null</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Base types count, because both frameworks inherit the attribute: a derived fixture of an
    ///     annotated base is discovered without repeating it. The name returned is the attribute the
    ///     message quotes, so a reader is told which declaration is being contradicted.
    /// </remarks>
    static string? Declares(INamedTypeSymbol type, TestFrameworks frameworks) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (TestFrameworks.Carries(current, frameworks.MsTestClassAttribute)) {
                return "TestClass";
            }

            if (TestFrameworks.Carries(current, frameworks.NUnitFixtureAttribute)) {
                return "TestFixture";
            }
        }

        return null;
    }
}
