using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7101</c> — <c>SK7010</c> with the accessibility filter turned round: a declaration that is not
///     publicly visible and carries no documentation comment.
/// </summary>
/// <remarks>
///     ⚠ <b>It ships at <c>none</c>, and the fire count is the argument for that rather than against it.</b>
///     The inspection this covers is the highest-firing uncovered one in the whole parity measurement —
///     3 408 findings — and <c>SK7010</c>, which is the same predicate over the public surface alone, already
///     produces 1 868 on <c>Testing/corpus</c> at <c>warning</c>. Enabled by default this would bury every
///     other finding in the report, which is the failure docs/plan/16 § R3 names and the outcome
///     <c>SK7010</c>'s own rationale describes: "thousands of findings on day one and switched off by
///     lunchtime". A repository turns it on for the paths where it means something —
///     <c>dotnet_diagnostic.SK7101.severity</c> in a scoped <c>.editorconfig</c> section — exactly as
///     <c>SK7010</c> is meant to be used.
///     <para>
///         ⚠
///         <b>
///             A separate analyzer rather than a seventh branch of <see cref="MetricsAnalyzer" />, and the
///             reason is the severity.
///         </b> That class exists so the per-member metrics are computed in one visit
///         instead of seven, which is the right shape for rules that always run. This one is disabled by
///         default, and Roslyn does not run an analyzer whose every diagnostic is suppressed — so as its own
///         analyzer it costs nothing in the repositories that have not asked for it, and inside
///         <c>MetricsAnalyzer</c> it would cost a predicate on every member in every compilation.
///     </para>
///     <para>
///         ⚠ The predicates are <see cref="MemberMetrics" />'s, unchanged, with
///         <see cref="MemberMetrics.IsPublicApi" /> negated. That is deliberate: the pair is meant to
///         partition the same population, so a member cannot be reported by both or by neither because the
///         two rules disagree about what a documentable member is.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UndocumentedNonPublicMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NonPublicMemberNotDocumented);

    static readonly ImmutableArray<SyntaxKind> Kinds = ImmutableArray.Create(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, Kinds);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = context.Node;
        if (!MemberMetrics.IsDocumentable(declaration)
            || MemberMetrics.IsPublicApi(declaration)
            || MemberMetrics.HasDocumentation(declaration)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Name(declaration).GetLocation(),
                "`" + Name(declaration).ValueText + "` is not public API and has no documentation comment"
            )
        );
    }

    static SyntaxToken Name(SyntaxNode declaration) =>
        declaration switch {
            MethodDeclarationSyntax method => method.Identifier,
            ConstructorDeclarationSyntax constructor => constructor.Identifier,
            PropertyDeclarationSyntax property => property.Identifier,
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
            EventDeclarationSyntax declaredEvent => declaredEvent.Identifier,
            DelegateDeclarationSyntax declaredDelegate => declaredDelegate.Identifier,
            BaseTypeDeclarationSyntax type => type.Identifier,
            _ => declaration.GetFirstToken()
        };
}
