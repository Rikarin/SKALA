using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>SK7030: physical file lines, with an optional terminal empty line excluded.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileLengthAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FileLength);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CompilationUnit);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var tree = context.Node.SyntaxTree;
        var text = tree.GetText(context.CancellationToken);
        var count = text.Lines.Count;
        if (text.Lines[count - 1].Span.Length == 0) {
            count--;
        }

        var threshold = MetricThresholds.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree)).FileLines;
        if (count <= threshold) {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            MemberMetrics.ValueKey,
            count.ToString(CultureInfo.InvariantCulture)
        );
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, new TextSpan(0, 0)),
                properties,
                "The file has "
                + count.ToString(CultureInfo.InvariantCulture)
                + " lines, over the threshold of "
                + threshold.ToString(CultureInfo.InvariantCulture)
            )
        );
    }
}
