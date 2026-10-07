using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Rules;
using System.Globalization;
using System.Text;
using ExpressionSyntaxNode = Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax;

namespace Rikarin.Skala.Testing;

/// <summary>
///     #432's measurement: the captured arguments the formatter's syntactic answer preserves against the
///     ones the compiler actually captures, over <c>corpus/real/</c>, and how many of each the oracle
///     rewrites.
/// </summary>
/// <remarks>
///     ⚠ "The oracle rewrites it" is the fidelity cost of preserving it, measured per span rather than
///     per line: the span's first and last tokens are found in the committed format-only fixture by
///     token ordinal — the fixture has the input's token stream — and the text between them compared
///     with the input's. A file whose fixture's token stream differs is counted and skipped, never
///     guessed at.
///     <para>
///         ⚠ The semantic side is <see cref="CallerArgumentSafety.CapturedTextSpans" /> over
///         <see cref="RuleCorpus.Compile" />: the shared framework and the SDK's implicit usings and
///         nothing else, so a capturing API from a package the corpus references (NUnit, say) is not
///         seen by it. Its count is a floor on what a real build would capture.
///     </para>
/// </remarks>
public static class CapturedArgumentSurvey {
    public static string Run() {
        var report = new StringBuilder();
        report.AppendLine("tree         files  syntactic  semantic  both  syntactic-only  semantic-only  skipped");
        report.AppendLine("  oracle rewrites:   syntactic  semantic  syntactic-only  semantic-only");

        var missed = new List<string>();
        var extra = new List<string>();
        var rewritten = new List<string>();

        foreach (var tree in RuleCorpus.Trees()) {
            var compilation = RuleCorpus.Compile(tree);
            var sources = RuleCorpus.Sources(tree)
                .ToDictionary(static file => Path.GetFullPath(file.Path), StringComparer.Ordinal);

            int files = 0, skipped = 0, unresolved = 0;
            int syntacticCount = 0, semanticCount = 0, both = 0, syntacticOnly = 0, semanticOnly = 0;
            int syntacticRewritten = 0, semanticRewritten = 0, syntacticOnlyRewritten = 0, semanticOnlyRewritten = 0;

            foreach (var syntaxTree in compilation.SyntaxTrees) {
                if (!sources.TryGetValue(syntaxTree.FilePath, out var file)) {
                    continue;
                }

                files++;
                var root = syntaxTree.GetRoot();
                var model = compilation.GetSemanticModel(syntaxTree);
                var syntactic = CapturedArguments.Find(root).ToHashSet();
                var semantic = CallerArgumentSafety.CapturedTextSpans(model, CancellationToken.None).ToHashSet();
                var oracle = file.HasFixture ? Oracle(root, File.ReadAllText(file.ExpectedPath)) : null;
                if (oracle is null) {
                    skipped++;
                }

                syntacticCount += syntactic.Count;
                semanticCount += semantic.Count;
                foreach (var span in syntactic) {
                    var changed = oracle?.Invoke(span) == true;
                    syntacticRewritten += changed ? 1 : 0;
                    if (semantic.Contains(span)) {
                        both++;
                        continue;
                    }

                    syntacticOnly++;
                    syntacticOnlyRewritten += changed ? 1 : 0;

                    // ⚠ The corpus does not compile, and a call whose argument binds to an error type
                    // has no IInvocationOperation to ask — so a syntactic-only span is either a real
                    // over-approximation or a semantic false negative, and this says which.
                    var unbound = root.FindNode(span, getInnermostNodeForTie: true) is ExpressionSyntaxNode expression
                        && model.GetTypeInfo(expression).Type is null or { TypeKind: TypeKind.Error };
                    unresolved += unbound ? 1 : 0;
                    extra.Add(
                        Describe(file, root, span, changed) + (unbound ? "  [argument type unresolved]" : string.Empty)
                    );
                }

                foreach (var span in semantic) {
                    var changed = oracle?.Invoke(span) == true;
                    semanticRewritten += changed ? 1 : 0;
                    if (changed) {
                        rewritten.Add(Describe(file, root, span, changed));
                    }

                    if (syntactic.Contains(span)) {
                        continue;
                    }

                    semanticOnly++;
                    semanticOnlyRewritten += changed ? 1 : 0;
                    missed.Add(Describe(file, root, span, changed));
                }
            }

            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"{tree,-12} {files,5}  {syntacticCount,9}  {semanticCount,8}  {both,4}  "
                + $"{syntacticOnly,14}  {semanticOnly,13}  {skipped,7}"
            );
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"  oracle rewrites:   {syntacticRewritten,9}  {semanticRewritten,8}  "
                + $"{syntacticOnlyRewritten,14}  {semanticOnlyRewritten,13}"
            );
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"  syntactic-only whose argument's type does not resolve in this compilation: {unresolved}"
            );
        }

        Section(report, "captured by the compiler, not preserved by the syntactic answer", missed);
        Section(report, "preserved by the syntactic answer, not captured by the compiler", extra);
        Section(report, "captured spans the oracle rewrites", rewritten);
        return report.ToString();
    }

    static void Section(StringBuilder report, string title, List<string> lines) {
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"── {title} ({lines.Count})");
        foreach (var line in lines) {
            report.AppendLine(line);
        }
    }

    static string Describe(CorpusFile file, SyntaxNode root, TextSpan span, bool changed) {
        var line = root.SyntaxTree.GetLineSpan(span).StartLinePosition.Line + 1;
        var text = root.SyntaxTree.GetText().ToString(span).ReplaceLineEndings("⏎");
        if (text.Length > 100) {
            text = text[..100] + "…";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"  {file.RelativePath}:{line}{(changed ? "  [oracle rewrites]" : string.Empty)}  {text}"
        );
    }

    /// <summary>
    ///     A question "does the oracle's fixture hold this span's text unchanged", or null when the
    ///     fixture's token stream is not the input's.
    /// </summary>
    static Func<TextSpan, bool>? Oracle(SyntaxNode root, string expected) {
        var fixture = CSharpSyntaxTree.ParseText(expected, CSharpFormatter.ParseOptions).GetRoot();
        var before = root.DescendantTokens().ToArray();
        var after = fixture.DescendantTokens().ToArray();
        if (before.Length != after.Length) {
            return null;
        }

        for (var i = 0; i < before.Length; i++) {
            if (before[i].RawKind != after[i].RawKind || before[i].Text != after[i].Text) {
                return null;
            }
        }

        var starts = new Dictionary<int, int>();
        var ends = new Dictionary<int, int>();
        for (var i = 0; i < before.Length; i++) {
            starts.TryAdd(before[i].SpanStart, i);
            ends[before[i].Span.End] = i;
        }

        var source = root.ToFullString();
        return span => {
            if (!starts.TryGetValue(span.Start, out var first) || !ends.TryGetValue(span.End, out var last)) {
                return false;
            }

            var mapped = TextSpan.FromBounds(after[first].SpanStart, after[last].Span.End);
            return !string.Equals(
                source.Substring(span.Start, span.Length),
                expected.Substring(mapped.Start, mapped.Length),
                StringComparison.Ordinal
            );
        };
    }
}
