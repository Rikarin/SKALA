using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4024</c> — <c>GC.Collect</c> called from code that is not measuring the collector.
/// </summary>
/// <remarks>
///     ⚠ Reports and does not fix, deliberately. Deleting the call is a one-token edit and it is not
///     the repair: the call is standing in for an allocation, a buffer or a handle, and removing it
///     without dealing with that changes the program's memory behaviour in a direction nobody
///     measured. docs/plan/08 leaves room for the fixless form for exactly this shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForcedGarbageCollectionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForcedGarbageCollection);

    /// <summary>
    ///     ⚠ The attributes that mean "this method exists to measure", where a forced collection is the
    ///     point rather than the defect.
    /// </summary>
    static readonly HashSet<string> Measurement = new(System.StringComparer.Ordinal) {
        "FactAttribute",
        "TheoryAttribute",
        "TestAttribute",
        "TestCaseAttribute",
        "TestMethodAttribute",
        "BenchmarkAttribute",
        "GlobalSetupAttribute",
        "GlobalCleanupAttribute",
        "IterationSetupAttribute",
        "IterationCleanupAttribute",
        "SetUpAttribute",
        "TearDownAttribute",
        "TestInitializeAttribute",
        "TestCleanupAttribute"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var cancellation = context.CancellationToken;
        if (context.SemanticModel.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                Name: "Collect",
                IsStatic: true,
                ContainingType: { Name: "GC" } container
            }
            || container.ContainingNamespace?.ToDisplayString() != "System"
            || container.ContainingType is not null
            || Measures(context.SemanticModel, invocation, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`GC.Collect` suspends every thread and promotes what survives; remove it and address what it "
                + "was compensating for"
            )
        );
    }

    static bool Measures(SemanticModel model, SyntaxNode node, System.Threading.CancellationToken cancellation) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is not MemberDeclarationSyntax declaration) {
                continue;
            }

            if (model.GetDeclaredSymbol(declaration, cancellation) is { } symbol
                && symbol.GetAttributes()
                    .Any(static attribute => attribute.AttributeClass is { } type
                        && Measurement.Contains(type.MetadataName)
                    )) {
                return true;
            }
        }

        return false;
    }
}
