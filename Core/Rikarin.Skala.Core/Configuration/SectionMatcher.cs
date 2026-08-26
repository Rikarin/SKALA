using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
/// Answers "does this <c>[glob]</c> apply to this file" using Roslyn's own matcher.
/// </summary>
/// <remarks>
/// ADR-001 requires that section globbing match the compiler exactly. Roslyn's
/// <c>SectionNameMatcher</c> is internal, so the question is asked through the public API instead:
/// a one-section <c>AnalyzerConfigSet</c> rooted at the real config's directory, carrying a single
/// probe key. If the probe comes back, the section matched. The template has three sections, so
/// this is three parses per config, cached.
/// </remarks>
public static class SectionMatcher {
    const string ProbeKey = "skala_section_probe";

    static readonly ConcurrentDictionary<(string Directory, string Section), AnalyzerConfigSet> Probes = new();

    public static bool Matches(EditorConfigSection section, string sourcePath) {
        if (section.Name is null) {
            // The preamble is not a section: only `root` lives there and it applies to the walk,
            // not to a file.
            return false;
        }

        var set = Probes.GetOrAdd((section.Document.Directory, section.Name), static key => {
            var text = SourceText.From($"root = true{Environment.NewLine}[{key.Section}]{Environment.NewLine}{ProbeKey} = 1{Environment.NewLine}");
            var config = AnalyzerConfig.Parse(text, Path.Combine(key.Directory, EditorConfigDocument.FileName));
            return AnalyzerConfigSet.Create(new[] { config });
        });

        return set.GetOptionsForSourcePath(Path.GetFullPath(sourcePath)).AnalyzerOptions.ContainsKey(ProbeKey);
    }

    /// <summary>
    /// The effective option map the compiler itself would produce for <paramref name="sourcePath"/>
    /// from this chain. Used to cross-check Skala's own resolution, never as its source.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CompilerView(IEnumerable<EditorConfigDocument> chain, string sourcePath) {
        var configs = chain
            .Select(static document => AnalyzerConfig.Parse(SourceText.From(document.Text), document.Path))
            .ToArray();

        return AnalyzerConfigSet.Create(configs).GetOptionsForSourcePath(Path.GetFullPath(sourcePath)).AnalyzerOptions;
    }
}
