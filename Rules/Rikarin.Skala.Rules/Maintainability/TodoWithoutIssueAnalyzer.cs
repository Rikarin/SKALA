using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary><c>SK7040</c> — a TODO or FIXME comment with no issue reference.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TodoWithoutIssueAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TodoWithoutIssueReference);

    static readonly Regex Marker = new(
        @"^[ \t]*(?:/{2,3}|/\*+|\*+)[ \t]*(?<marker>TODO|FIXME)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
    );

    static readonly Regex Issue = new(
        @"(?:https?://\S+|#\d+\b|\b[A-Z][A-Z0-9]{1,15}-\d+\b)",
        RegexOptions.Compiled
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true)) {
            if (!IsComment(trivia)) {
                continue;
            }

            var text = trivia.ToFullString();
            foreach (Match match in Marker.Matches(text)) {
                var lineEnd = text.IndexOf('\n', match.Index);
                var line = lineEnd < 0
                    ? text.Substring(match.Index)
                    : text.Substring(match.Index, lineEnd - match.Index);
                if (Issue.IsMatch(line)) {
                    continue;
                }

                var marker = match.Groups["marker"];
                var span = new Microsoft.CodeAnalysis.Text.TextSpan(
                    trivia.FullSpan.Start + marker.Index,
                    marker.Length
                );
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        Location.Create(context.Tree, span),
                        "`" + marker.Value.ToUpperInvariant() + "` has no issue reference"
                    )
                );
            }
        }
    }

    static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia);
}
