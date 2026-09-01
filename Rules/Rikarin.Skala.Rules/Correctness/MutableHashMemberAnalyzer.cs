using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2043</c> — the hash code depends on state that can change.</summary>
/// <remarks>
///     ⚠ Classes only. A struct is copied into the collection, so mutating the original cannot move
///     the stored copy; the hazard needs reference identity to exist at all.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableHashMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MutableHashCodeMember);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var contract = HashCodeContract.Resolve(
            (TypeDeclarationSyntax)context.Node,
            context.SemanticModel,
            context.CancellationToken
        );

        if (!contract.Valid) {
            return;
        }

        foreach (var pair in contract.Compared) {
            if (!contract.CanChangeAfterConstruction(pair.Key, context.SemanticModel, context.CancellationToken)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    pair.Value,
                    "the hash code reads `"
                    + pair.Key.Name
                    + "`, which can be assigned after construction, so a stored instance stops being findable"
                )
            );
        }
    }
}
