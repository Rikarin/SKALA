using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7073</c> — a <c>#region</c> with nothing in it.
/// </summary>
/// <remarks>
///     A region survives every deletion inside it, so an empty one is the residue of a member that
///     moved or went away. It is a heading over nothing, and the outline it produces claims structure
///     the file does not have.
///     <para>
///         ⚠ Only whitespace counts as nothing. A comment, a <c>#pragma</c>, inactive <c>#if</c> text —
///         all of it is content and the rule stays quiet, because a region that still says something to
///         a reader is not the residue this is about. The one exception is a nested region, so that
///         <c>#region</c>/<c>#region</c>/<c>#endregion</c>/<c>#endregion</c> is a single edit rather
///         than two <c>skala fix</c> passes.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyRegionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptyRegion);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var regions = root.DescendantNodes(descendIntoTrivia: true)
            .OfType<RegionDirectiveTriviaSyntax>()
            .ToList();
        if (regions.Count == 0) {
            return;
        }

        var source = context.Tree.GetText(context.CancellationToken);
        var content = RegionContent.Positions(
            root,
            SyntaxKind.RegionDirectiveTrivia,
            SyntaxKind.EndRegionDirectiveTrivia
        );

        foreach (var region in regions) {
            // ⚠ An unbalanced `#region` has no partner and is skipped. Deleting one half of a pair
            // the parser could not match would be an edit made on a guess.
            if (region.GetRelatedDirectives().OfType<EndRegionDirectiveTriviaSyntax>().FirstOrDefault()
                is not { } end) {
                continue;
            }

            var span = TextSpan.FromBounds(region.FullSpan.End, end.FullSpan.Start);
            if (span.Length > 0 && content.Any(span.Contains)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    region.GetLocation(),
                    FixEdits.Pack((Line(source, region), string.Empty), (Line(source, end), string.Empty)),
                    "the region holds nothing: it is a heading over no code"
                )
            );
        }
    }

    static TextSpan Line(SourceText source, DirectiveTriviaSyntax directive) {
        var line = source.Lines.GetLineFromPosition(directive.SpanStart);
        return TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak);
    }
}
