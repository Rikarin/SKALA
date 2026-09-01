using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary><c>SK7050</c> — a warning-disable pragma with no adjacent justification.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PragmaWithoutJustificationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PragmaWithoutJustification);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var source = context.Tree.GetText(context.CancellationToken);
        foreach (var directive in root.DescendantNodes(descendIntoTrivia: true)
                     .OfType<PragmaWarningDirectiveTriviaSyntax>()) {
            if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)
                || HasJustification(source, directive)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    directive.GetLocation(),
                    "warning suppression has no adjacent justification comment"
                )
            );
        }
    }

    static bool HasJustification(
        Microsoft.CodeAnalysis.Text.SourceText source,
        PragmaWarningDirectiveTriviaSyntax directive
    ) {
        var line = source.Lines.GetLineFromPosition(directive.SpanStart);
        if (MeaningfulComment(line.ToString())) {
            return true;
        }

        for (var index = line.LineNumber - 1; index >= 0; index--) {
            var previous = source.Lines[index].ToString().Trim();
            if (previous.Length == 0) {
                continue;
            }

            return MeaningfulComment(previous, true);
        }

        return false;
    }

    static bool MeaningfulComment(string line, bool wholeLine = false) {
        var marker = line.IndexOf("//", StringComparison.Ordinal);
        if (marker >= 0) {
            return Meaningful(line.Substring(marker + 2));
        }

        marker = line.IndexOf("/*", StringComparison.Ordinal);
        if (marker >= 0) {
            return Meaningful(line.Substring(marker + 2).Replace("*/", string.Empty));
        }

        return wholeLine
            && line.StartsWith("*", StringComparison.Ordinal)
            && Meaningful(line.TrimStart('*'));
    }

    static bool Meaningful(string text) {
        var value = text.Trim();
        return value.Length > 0
            && !value.StartsWith("TODO", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("FIXME", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("TBD", StringComparison.OrdinalIgnoreCase);
    }
}
