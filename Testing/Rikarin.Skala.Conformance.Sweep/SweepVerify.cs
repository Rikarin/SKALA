using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
/// One option's row in the sweep table, expanded into the text both engines actually produced.
/// </summary>
/// <remarks>
/// ⚠ A verdict nobody can check is a verdict nobody should act on, and the sweep's headline output
/// is a table of 97 rows saying which engine moved. Demoting an option out of Tier A on the strength
/// of one of those rows means reading the row first: this runs a single option, unbatched, and
/// prints the oracle's and Skala's output at every value beside the input, so <c>SPURIOUS</c> can be
/// confirmed as "ReSharper genuinely ignores this key on this fixture" rather than accepted as
/// "the harness said so".
/// </remarks>
public static class SweepVerify {
    public static int Run(SweepPlanResult plan, string key, string baseConfigPath, TextWriter output) {
        var candidate = plan.Candidates.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        if (candidate is null) {
            output.WriteLine("not swept: " + key);
            foreach (var exclusion in plan.Excluded.Where(e => string.Equals(e.Info.Key, key, StringComparison.Ordinal)
                     )) {
                output.WriteLine("  " + exclusion.Reason);
            }

            return 1;
        }

        var runner = new OracleRunner();
        var sweep = new KeyFlipSweep(runner, baseConfigPath, TextWriter.Null);

        output.WriteLine(candidate.Key);
        output.WriteLine("  fixture: " + candidate.Fixture);
        output.WriteLine("  tier:    " + candidate.Info.Tier);
        output.WriteLine("  values:  " + string.Join(", ", candidate.Values));
        output.WriteLine();

        // ⚠ One value per invocation, unbatched. The batching is what makes a whole sweep
        // affordable; it is also the part a suspicious verdict most wants ruled out, so the
        // confirmation deliberately does not use it.
        var oracle = new List<string?>();
        var skala = new List<string>();
        for (var round = 0; round < candidate.Values.Count; round++) {
            var body = ScratchTree.Format(runner, [candidate], c => sweep.ConfigFor(c.Key, c.Values[round]))[0];
            oracle.Add(body is null ? null : TextNormalisation.Normalise(body));

            var resolved = OptionResolver.Resolve(
                candidate.Fixture.Path,
                [new KeyValuePair<string, string>(candidate.Key, candidate.Values[round])]
            );
            skala.Add(
                TextNormalisation.Normalise(
                    CSharpFormatter.Format(
                        candidate.Fixture.Path,
                        CSharpFormatter.Read(candidate.Fixture.Path),
                        resolved.Options
                    )
                        .Formatted
                )
            );

            // ⚠ Normalised on both sides here, matching the sweep's primary comparison. An option
            // whose whole effect is the line terminator is reported by the sweep as `LineEndingOnly`
            // and this view cannot show it; that is stated rather than papered over.
        }

        for (var i = 0; i < candidate.Values.Count; i++) {
            output.WriteLine("── " + candidate.Key + " = " + candidate.Values[i] + " ──");
            output.WriteLine("  oracle:");
            output.WriteLine(Indent(oracle[i] ?? "(cleanupcode produced nothing)"));
            output.WriteLine("  skala:");
            output.WriteLine(Indent(skala[i]));
            output.WriteLine(
                "  " + (string.Equals(oracle[i], skala[i], StringComparison.Ordinal) ? "agree" : "DISAGREE")
            );
            output.WriteLine();
        }

        var oracleDistinct = oracle.Where(static body => body is not null).Distinct(StringComparer.Ordinal).Count();
        var skalaDistinct = skala.Distinct(StringComparer.Ordinal).Count();
        var agreements = Enumerable.Range(0, candidate.Values.Count)
            .Count(i => string.Equals(oracle[i], skala[i], StringComparison.Ordinal));

        output.WriteLine(
            "oracle produced "
            + oracleDistinct.ToString(CultureInfo.InvariantCulture)
            + " distinct outputs, Skala "
            + skalaDistinct.ToString(CultureInfo.InvariantCulture)
            + "; they agree at "
            + agreements.ToString(CultureInfo.InvariantCulture)
            + " of "
            + candidate.Values.Count.ToString(CultureInfo.InvariantCulture)
            + " values"
        );
        output.WriteLine(
            "verdict: " + OptionSweep.Classify(oracleDistinct, skalaDistinct, agreements, candidate.Values.Count)
        );
        return 0;
    }

    static string Indent(string text) {
        var builder = new StringBuilder();
        foreach (var line in TextNormalisation.Lines(text)) {
            builder.Append("    | ").AppendLine(line);
        }

        return builder.ToString().TrimEnd('\n');
    }
}
