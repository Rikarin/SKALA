using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2042</c> — the hash code reads state that equality ignores.</summary>
/// <remarks>
///     ⚠ This is the direction that is a contract violation. Equal objects must hash the same, so a
///     hash over a member equality ignores loses the object inside its own dictionary. The reverse —
///     an <c>Equals</c> comparing more than the hash reads — only weakens the hash and is not
///     reported; see the rule's <c>falsePositives</c>, which records that refusal.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UncomparedHashMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.HashCodeOverUncomparedMember);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
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

        foreach (var pair in contract.Uncompared) {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    pair.Value,
                    "the hash code reads `"
                    + pair.Key.Name
                    + "` and no Equals compares it, so two equal instances can hash differently"
                )
            );
        }
    }
}
