using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>
///     Skala's documentation-comment output against the oracle profile that formats documentation
///     comments.
/// </summary>
/// <remarks>
///     ⚠ This measurement could not be taken for six milestones, and the reason was a profile rather
///     than a tool. Every committed <c>.expected.cs</c> was generated under
///     <see cref="OracleProfile.FormatOnly" />, which is byte-for-byte ReSharper's
///     <c>Built-in: Reformat Code</c> — the one built-in profile that switches
///     <c>CSharpFormatDocComments</c> off. SK-DIV-0006 read the resulting silence as "the oracle
///     declines to format documentation comments", and 22 option keys were held at Tier D on the
///     strength of it. <see cref="OracleProfile.DocComments" /> asks the question, and this compares
///     the answers.
///     <para>
///         ⚠ One file per key, and the comparison is byte-for-byte over the whole file rather than over
///         a hand-picked span. A per-key measurement that scored only the lines the key is "supposed to"
///         move would be Skala marking its own homework: the interesting failures are the ones where a
///         key is honoured and something beside it is not.
///     </para>
/// </remarks>
public static class XmlDocOracle {
    /// <summary>One corpus file's verdict under the doc-comment profile.</summary>
    public sealed record Row(CorpusFile File, string Expected, string Actual) {
        public bool Agrees =>
            string.Equals(
                TextNormalisation.Normalise(Expected),
                TextNormalisation.Normalise(Actual),
                StringComparison.Ordinal
            );

        /// <summary>The option key this file is named after, or null when it is not named after one.</summary>
        public string Key => System.IO.Path.GetFileNameWithoutExtension(File.Path);
    }

    /// <summary>Every doc-commented corpus file with a committed doc-comment fixture, measured.</summary>
    public static IReadOnlyList<Row> Rows() {
        var rows = new List<Row>();
        foreach (var file in Corpus.DocCommented()) {
            if (!file.HasFixtureFor(OracleProfile.DocComments)) {
                continue;
            }

            var text = CSharpFormatter.Read(file.Path);
            var options = OptionResolver.Resolve(file.Path).Options;
            rows.Add(
                new Row(
                    file,
                    OracleFixture.Read(file, OracleProfile.DocComments),
                    CSharpFormatter.Format(file.Path, text, options).Formatted
                )
            );
        }

        return rows;
    }

    public static string Measure() {
        var rows = Rows();
        var builder = new StringBuilder();
        builder.AppendLine("── constructs/xmldoc ── Skala against the SkalaDocComments profile ──");
        builder.AppendLine();

        foreach (var row in rows.OrderBy(static row => row.Key, StringComparer.Ordinal)) {
            builder.Append(row.Agrees ? "  agrees    " : "  DIVERGES  ").AppendLine(row.Key);
        }

        builder.AppendLine();
        builder.Append(rows.Count(static row => row.Agrees).ToString(CultureInfo.InvariantCulture))
            .Append(" of ")
            .Append(rows.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" files agree byte for byte.");

        foreach (var row in rows.Where(static row => !row.Agrees)
                     .OrderBy(static row => row.Key, StringComparer.Ordinal)) {
            builder.AppendLine();
            builder.Append("──── ").AppendLine(row.Key);
            foreach (var line in Diff(row)) {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    /// <summary>A unified-ish diff of the two, so a divergence arrives with its shape attached.</summary>
    public static IReadOnlyList<string> Diff(Row row) {
        var expected = TextNormalisation.Lines(row.Expected);
        var actual = TextNormalisation.Lines(row.Actual);
        var lines = new List<string>();
        foreach (var entry in LineDiff.Compute(expected, actual)) {
            switch (entry.Kind) {
                case LineDiff.Kind.Same:
                    break;
                case LineDiff.Kind.Removed:
                    lines.Add("  oracle │ " + entry.Line);
                    break;
                default:
                    lines.Add("  skala  │ " + entry.Line);
                    break;
            }
        }

        return lines;
    }
}
