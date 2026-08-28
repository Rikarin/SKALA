using System.Text.Json;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The committed key-flip sweep's verdicts, for the tests that may not claim Tier A without them.
/// </summary>
/// <remarks>
///     ⚠ <b>One reader, because three tests need this answer and the third one got it wrong.</b>
///     <c>OptionCoverageTests</c> already read the archive to decide tiers. <c>XmlDocOracleTests</c> and
///     <c>XmlDocFormatterTests</c> did not, and each asserted its own version of "the fixture agrees, so
///     the key is Tier A" — which is the one-configuration fallacy the sweep exists to refute, restated
///     inside a newer test.
///     <para>
///         ⚠ <b>The measurement that settled it.</b> Six <c>resharper_xmldoc_*</c> keys were promoted to
///         Tier A on a doc-comment fixture and demoted again when the sweep reached them. Both
///         measurements were right and they answered different questions: every one of the six
///         <em>agrees at the export's value</em> and diverges only away from it — <c>indent_size</c> at
///         <c>1</c>, <c>indent_style</c> at <c>tab</c>, two <c>linebreaks_inside_tags_*</c> at
///         <c>false</c>, <c>max_blank_lines_between_tags</c> at <c>1</c>. A fixture pins one
///         configuration; Tier A is a claim about the option.
///     </para>
///     <para>
///         ⚠ A missing report returns empty, which <em>restores</em> the strict invariant rather than
///         relaxing it: with no sweep to appeal to, nothing is excused and every implemented key must be
///         Tier A. "Nobody has measured" and "measured and disagreed" are opposite states and this file
///         must not collapse them.
///     </para>
/// </remarks>
public static class SweepVerdicts {
    public static string ArchivePath { get; } = Path.Combine(
        Corpus.RepositoryRoot,
        "Testing",
        "Rikarin.Skala.Conformance.Sweep",
        "conformance-sweep.json"
    );

    /// <summary>Whether a sweep has been committed at all.</summary>
    public static bool HasReport => File.Exists(ArchivePath);

    /// <summary>
    ///     The keys the last committed sweep could not substantiate — anything not <c>Conformant</c>.
    /// </summary>
    public static IReadOnlySet<string> Unsubstantiated() {
        if (!HasReport) {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(ArchivePath));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.RootElement.EnumerateArray()) {
            if (!string.Equals(row.GetProperty("Outcome").GetString(), "Conformant", StringComparison.Ordinal)) {
                keys.Add(row.GetProperty("Key").GetString() ?? string.Empty);
            }
        }

        return keys;
    }
}
