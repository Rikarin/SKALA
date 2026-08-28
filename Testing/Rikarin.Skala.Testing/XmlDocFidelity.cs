using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>
///     What the documentation-comment sub-formatter costs against an oracle profile that never moves.
/// </summary>
/// <remarks>
///     ⚠ SK-DIV-0006 asserted the cost and nobody had measured it: "a Skala that re-wrapped them would
///     diverge from the oracle on every doc comment in the corpus". This turns that sentence into a
///     number, which is what this project does with sentences like it.
///     <para>
///         ⚠ What the number is <em>of</em> changed when the sub-formatter became the default. The oracle
///         does not decline to format documentation comments — the profile Skala pins does not ask it to.
///         <c>CSharpFormatDocComments</c> is a real <c>jb cleanupcode</c> task and
///         <see cref="OracleProfile.FormatOnly" /> does not enable it, so these rows measure a profile gap
///         and not a formatter defect. They are kept because the gap is real until the fixtures are
///         regenerated, and because the fourth row is the containment claim.
///     </para>
///     <para>
///         ⚠ <b>This is no longer the only doc-comment measurement, and it is no longer the interesting
///         one.</b> <c>harness xmldoc --oracle</c> (<see cref="XmlDocOracle" />) compares Skala against
///         <see cref="OracleProfile.DocComments" />, which does ask the question, over the
///         <c>constructs/xmldoc/</c> subtree. What is still only measurable <em>here</em> is
///         <c>corpus/real/</c>, because that set has no doc-comment fixture yet — so these four rows stay
///         until it does, and the gap they measure stays a gap in the fixtures rather than in the tool.
///     </para>
///     <para>
///         ⚠ The exclusion is drawn the only honest way: every <c>///</c> line is removed from <b>both</b>
///         sides before the comparison. Not "the lines Skala changed" — that would be marking one's own
///         homework — and not "the files with doc comments", which would hide a real regression in the code
///         around them. What is left is every line of the corpus the sub-formatter is not allowed to touch,
///         and it may not move at all.
///     </para>
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

            var without = CSharpFormatter.Format(file.Path, text, options, null, null, xmlDoc: false).Formatted;
            var with = CSharpFormatter.Format(file.Path, text, options).Formatted;

            plain.Add((file.ToString(), expected, without));
            reflowed.Add((file.ToString(), expected, with));
            outside.Add((file.ToString(), expected, with));
            baseline.Add((file.ToString(), expected, without));

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
        Row(builder, "--no-xmldoc, every line", Fidelity.Compare(plain, FidelityBasis.EveryLine));
        Row(builder, "default, every line", Fidelity.Compare(reflowed, FidelityBasis.EveryLine));
        Row(builder, "--no-xmldoc, outside doc comments", Fidelity.Compare(baseline));
        Row(builder, "default, outside doc comments", Fidelity.Compare(outside));
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
            .Append(", files the sub-formatter changes: ")
            .Append(changed.ToString(CultureInfo.InvariantCulture))
            .Append(" of ")
            .AppendLine(files.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var site in sites) {
            builder.AppendLine(site);
        }

        return builder.ToString();
    }

    static void Row(StringBuilder builder, string label, FidelityReport report) {
        builder.Append("  ")
            .Append(label.PadRight(36))
            .Append((report.LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .Append(" % ")
            .Append((report.FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .AppendLine(" %");
    }
}
