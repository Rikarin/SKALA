using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using System.Globalization;
using System.Text;

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

        /// <summary>
        ///     Whether this row is attributed to an option key: true exactly for a file under
        ///     <c>constructs/xmldoc/</c>.
        /// </summary>
        /// <remarks>
        ///     ⚠ <b>Attribution is by subtree, not by file name (#396).</b> A doc-comment row outside
        ///     <c>xmldoc/</c> is a <em>shape</em> row: it is compared byte for byte like every other row and
        ///     its disagreement fails a test, but it carries no key and no tier verdict. The file name is
        ///     not evidence of a key there — <c>trivia/skala_space_after_triple_slash.cs</c> is named after
        ///     one and pins nothing about it — and a shape file such as <c>syntax/cref-member-forms.cs</c>
        ///     has no key to name. Only <c>xmldoc/</c> enforces one file per key
        ///     (<c>EveryDocCommentedFile_IsNamedAfterARegistryKey</c>), so only there does the name mean
        ///     anything, and a tier judged off a name nothing enforces is a verdict attributed to whatever
        ///     the file happened to be called.
        /// </remarks>
        public bool IsKeyed => File.RelativePath.StartsWith(Corpus.XmlDocPrefix, StringComparison.Ordinal);

        /// <summary>The option key this row is attributed to, or null for a shape row.</summary>
        public string? Key => IsKeyed ? Path.GetFileNameWithoutExtension(File.Path) : null;

        /// <summary>The key for a keyed row, the corpus path for a shape row: what a report prints.</summary>
        public string Label => Key ?? File.RelativePath;
    }

    /// <summary>
    ///     Every construct holding a <c>///</c> line that has a committed doc-comment fixture, measured:
    ///     the keyed rows of <c>xmldoc/</c> and the shape rows outside it.
    /// </summary>
    public static IReadOnlyList<Row> Rows() {
        var rows = new List<Row>();
        foreach (var file in Corpus.DocCommentBearing().UnionBy(Corpus.DocCommented(), static file => file.Path)) {
            if (!file.HasFixtureFor(OracleProfile.DocComments)) {
                continue;
            }

            var text = CSharpFormatter.Read(file.Path);
            var options = OptionResolver.Resolve(file.Path).Options;
            rows.Add(
                new(
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
        builder.AppendLine("── constructs with a /// line ── Skala against the SkalaDocComments profile ──");
        builder.AppendLine();

        foreach (var row in rows.OrderBy(static row => !row.IsKeyed)
                     .ThenBy(static row => row.Label, StringComparer.Ordinal)) {
            builder.Append(row.Agrees ? "  agrees    " : "  DIVERGES  ").AppendLine(row.Label);
        }

        builder.AppendLine();
        builder.Append(rows.Count(static row => row.Agrees).ToString(CultureInfo.InvariantCulture))
            .Append(" of ")
            .Append(rows.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" files agree byte for byte.");

        foreach (var row in rows.Where(static row => !row.Agrees)
                     .OrderBy(static row => row.Label, StringComparer.Ordinal)) {
            builder.AppendLine();
            builder.Append("──── ").AppendLine(row.Label);
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
