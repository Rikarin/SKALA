using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7074</c> — a <c>goto</c> to a label.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         <c>goto case</c> and <c>goto default</c> do not fire, and that is the rule's position
///         rather than an oversight.
///     </b> They are the only way C# expresses switch fall-through, they
///     cannot leave the switch they are written in, and the control flow they describe is exactly the
///     one a reader already has in front of them. Reporting them would make the rule a style opinion
///     about <c>switch</c> and would be turned off with the part that is worth having.
///     <para>
///         A <c>goto</c> to a label is the other thing. Its target is anywhere in the enclosing member,
///         so a reader can no longer answer "what runs after this" from the nesting — and neither can
///         the tool: <c>SK7001</c> and <c>SK7002</c> compute complexity from the structure of the
///         statements, and a label jump is an edge those numbers do not have. That is why this rule
///         lives in the same range as the metrics it distorts.
///     </para>
///     <para>
///         Report-only. Breaking out of two loops at once is a real use with no mechanical rewrite: the
///         alternatives are a flag, an extracted method or an early return, and choosing between them
///         is a design decision about what the loop means. An edit that guessed would be worse than the
///         <c>goto</c>. Generated code is excluded, which is where nearly all remaining <c>goto</c> is.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnstructuredGotoAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnstructuredGoto);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ `GotoStatement` alone. `goto case` and `goto default` are `GotoCaseStatement` and
        // `GotoDefaultStatement`, distinct kinds, so the exclusion is in the registration rather
        // than in a filter somebody could later "simplify" away.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GotoStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (GotoStatementSyntax)context.Node;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GotoKeyword.GetLocation(),
                "`goto "
                + statement.Expression?.ToString()
                + "` jumps outside the statement nesting, so neither a reader nor the complexity "
                + "metrics can follow the control flow from the structure"
            )
        );
    }
}
