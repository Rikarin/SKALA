using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>
/// What the documentation-comment sub-formatter would cost against an oracle that never moves.
/// </summary>
/// <remarks>
/// ⚠ SK-DIV-0006 asserted the cost and nobody had measured it: "a Skala that re-wrapped them would
/// diverge from the oracle on every doc comment in the corpus". This turns that sentence into a
/// number, which is what this project does with sentences like it.
/// <para>
/// ⚠ The exclusion is the point of the second column, and it is drawn the only honest way: every
/// <c>///</c> line is removed from <b>both</b> sides before the comparison. Not "the lines Skala
/// changed" — that would be marking one's own homework — and not "the files with doc comments",
/// which would hide a real regression in the code around them. What is left is every line of the
/// corpus the sub-formatter is not allowed to touch, and it may not move at all.
/// </para>
/// </remarks>
public static class XmlDocFidelity {
    public static string Measure(string set = Corpus.Real) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        var plain = new List<(string File, string Expected, string Actual)>(files.Length);
        var reflowed = new List<(string File, string Expected, string Actual)>(files.Length);
        var outside = new List<(string File, string Expected, string Actual)>(files.Length);
        var baseline = new List<(string File, string Expected, string Actual)>(files.Length);

        var comments = 0;
        var changed = 0;
        var reasons = new SortedDictionary<XmlDocRefusalReason, int>();
        var sites = new List<string>();

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var options = OptionResolver.Resolve(file.Path).Options;
            var expected = OracleFixture.Read(file);

            var without = CSharpFormatter.Format(file.Path, text, options).Formatted;
            var with = CSharpFormatter.Format(file.Path, text, options, null, null, xmlDoc: true).Formatted;

            plain.Add((file.ToString(), expected, without));
            reflowed.Add((file.ToString(), expected, with));
            outside.Add((file.ToString(), WithoutDocLines(expected), WithoutDocLines(with)));
            baseline.Add((file.ToString(), WithoutDocLines(expected), WithoutDocLines(without)));

            var outcome = XmlDocFormatter.Rewrite(
                without,
                new XmlDocOptions(options),
                CSharpFormatter.ParseOptions,
                "\n"
            );

            comments += outcome.Reflowed + outcome.Refused;
            foreach (var refusal in outcome.Refusals) {
                reasons[refusal.Reason] = reasons.GetValueOrDefault(refusal.Reason) + 1;

                // ⚠ Always listed, never counted and forgotten. A RoundTrip refusal is the
                // sub-formatter saying it produced something it could not prove equivalent, and a
                // run that reports "14" without saying which has hidden its only real finding.
                if (refusal.Reason != XmlDocRefusalReason.Malformed) {
                    sites.Add(
                        "    "
                        + refusal.Reason
                        + "  "
                        + file
                        + ":"
                        + refusal.Line.ToString(CultureInfo.InvariantCulture)
                    );
                }
            }

            if (!string.Equals(without, with, StringComparison.Ordinal)) {
                changed++;
            }
        }

        var builder = new StringBuilder();
        builder.Append("── ").Append(set).AppendLine(" ── the xmldoc sub-formatter against the oracle ──");
        builder.AppendLine();
        builder.AppendLine("                                        line      file");
        Row(builder, "default (--xmldoc off)", Fidelity.Compare(plain));
        Row(builder, "--xmldoc, every line counted", Fidelity.Compare(reflowed));
        Row(builder, "default, /// lines excluded", Fidelity.Compare(baseline));
        Row(builder, "--xmldoc, /// lines excluded", Fidelity.Compare(outside));
        builder.AppendLine();
        builder.Append("doc comments seen: ")
            .Append(comments.ToString(CultureInfo.InvariantCulture))
            .Append(", left exactly as written: ")
            .Append(reasons.Values.Sum().ToString(CultureInfo.InvariantCulture))
            .Append(" [")
            .Append(
                string.Join(
                    ", ",
                    reasons.Select(static pair => pair.Key + " " + pair.Value.ToString(CultureInfo.InvariantCulture))
                )
            )
            .Append(']')
            .Append(", files the flag changes: ")
            .Append(changed.ToString(CultureInfo.InvariantCulture))
            .Append(" of ")
            .AppendLine(files.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var site in sites) {
            builder.AppendLine(site);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The text with every <c>///</c> line removed.
    /// </summary>
    /// <remarks>
    /// ⚠ A <c>///</c> inside a string literal would be removed too. It does not matter here and it
    /// would matter in a gate: this is a report, and the third row is only ever compared with
    /// itself across runs.
    /// </remarks>
    static string WithoutDocLines(string text) =>
        string.Join(
            '\n',
            TextNormalisation.Lines(text)
                .Where(static line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal))
        );

    static void Row(StringBuilder builder, string label, FidelityReport report) {
        builder.Append("  ")
            .Append(label.PadRight(36))
            .Append((report.LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .Append(" % ")
            .Append((report.FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .AppendLine(" %");
    }
}
