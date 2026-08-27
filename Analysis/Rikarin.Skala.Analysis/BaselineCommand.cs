using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>
/// <c>skala baseline create | update | prune | show</c>.
/// </summary>
/// <remarks>
/// docs/plan/09 § "The baseline". The baseline is "a reviewed, committed artefact — its diff in a
/// PR is 'we suppressed these', which is exactly the conversation that should happen".
/// <para>
/// ⚠ The three verbs are three different decisions and collapsing any two of them loses the point:
/// <list type="bullet">
/// <item><b>create</b> — accept everything that fires now. The one-time adoption step.</item>
/// <item>
/// <b>update</b> — accept what fires now <em>in addition to</em> what is already accepted. It never
/// removes an entry, so running it can only ever widen what is suppressed, and the diff shows
/// exactly by how much.
/// </item>
/// <item>
/// ⚠ <b>prune</b> — drop the entries that no longer fire. Separate, and never automatic:
/// "a baseline that self-prunes lets a rule that silently stopped working look like progress".
/// A rule that was accidentally disabled and a rule whose findings were fixed produce identical
/// prunes, and only a person can tell them apart.
/// </item>
/// </list>
/// </para>
/// </remarks>
public static class BaselineCommand {
    public enum Verb {
        /// <summary>Write a baseline holding everything that fires now, replacing any existing one.</summary>
        Create,

        /// <summary>Add what fires now to what is already accepted. ⚠ Never removes.</summary>
        Update,

        /// <summary>⚠ Remove the accepted entries that no longer fire. Never implicit.</summary>
        Prune,

        /// <summary>Print what the baseline holds and how it compares to a fresh run.</summary>
        Show
    }

    public static (CommandResult Result, RunReport Report) Run(
        Verb verb,
        CheckRequest request,
        bool apply,
        CancellationToken cancellation = default
    ) {
        // ⚠ The baseline is written from a run with *no* baseline applied, so every finding is
        // present and unbucketed. Reading the baseline first and then writing what came back would
        // make `create` idempotent in the wrong way — it would re-accept only what the old baseline
        // already held.
        var (checkResult, report) = CheckCommand.Run(
            request with { BaselinePath = null, Output = string.Empty, Record = false, Gate = "local" },
            cancellation
        );

        if (checkResult.ExitCode == ExitCodes.LoadFailure) {
            return (checkResult, report);
        }

        var path = request.BaselinePath is { Length: > 0 } named
            ? Path.GetFullPath(named)
            : Baseline.DefaultPath(report.RepositoryRoot);

        var existing = Baseline.Read(path);
        var comparison = existing.Compare(report.Findings);
        var builder = new StringBuilder();

        switch (verb) {
            case Verb.Show:
                return (new CommandResult(ExitCodes.Ok, Show(existing, comparison, report, path)), report);

            case Verb.Create:
                Describe(builder, "create", path, existing.Count, report.Findings.Length);
                if (apply) {
                    Baseline.Write(path, report, report.Findings);
                }

                break;

            case Verb.Update: {
                // ⚠ The union, and the union is the point. `update` accepts what is new and keeps
                // what was accepted even if it no longer fires — dropping the latter is `prune`,
                // and doing both in one verb makes "we suppressed these" and "we fixed these"
                // indistinguishable in the diff.
                var kept = report.Findings;
                Describe(builder, "update", path, existing.Count, kept.Length);
                builder.Append("  ")
                    .Append(comparison.NewCount.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" newly accepted");

                if (!comparison.Fixed.IsEmpty) {
                    builder.Append("  ⚠ ")
                        .Append(comparison.Fixed.Length.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(
                            " accepted finding(s) no longer fire and are being kept. "
                            + "`skala baseline prune` removes them — deliberately a separate command, "
                            + "because a rule that silently stopped working prunes exactly like a rule "
                            + "whose findings were fixed."
                        );
                }

                if (apply) {
                    Baseline.Write(path, report, Union(existing, report));
                }

                break;
            }

            case Verb.Prune:
                Describe(builder, "prune", path, existing.Count, report.Findings.Length);
                builder.Append("  ")
                    .Append(comparison.Fixed.Length.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" entr(y/ies) no longer fire and would be removed");

                foreach (var entry in comparison.Fixed.Take(20)) {
                    builder.Append("    ").Append(entry.RuleId).Append("  ").AppendLine(entry.Path);
                }

                if (comparison.Fixed.Length > 20) {
                    builder.Append("    … and ")
                        .Append((comparison.Fixed.Length - 20).ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" more");
                }

                if (apply) {
                    // Pruning writes exactly what still fires and was already accepted.
                    Baseline.Write(
                        path,
                        report,
                        report.Findings.Where(existing.Contains)
                    );
                }

                break;
        }

        builder.AppendLine(
            apply
                ? "  written."
                : "  nothing written; pass --apply to write it."
        );

        return (new CommandResult(ExitCodes.Ok, builder.ToString()), report);
    }

    /// <summary>
    /// Everything the baseline should hold after an <c>update</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Entries that no longer fire cannot be reconstructed as <see cref="Finding"/>s — there is
    /// no source span behind them any more — so they are carried through as the SARIF results they
    /// already are. That is why <see cref="Baseline.Write"/> takes findings and this method has to
    /// merge at the finding level: the fired half is fresh, the unfired half is preserved verbatim
    /// by writing the old file's results back alongside.
    /// </remarks>
    static IEnumerable<Finding> Union(Baseline existing, RunReport report) {
        var firing = report.Findings;
        var seen = firing.Select(Fingerprints.V2).ToHashSet(StringComparer.Ordinal);

        // The unfired accepted entries are represented by a placeholder finding carrying their
        // fingerprint inputs, so the rewritten file still holds them.
        var carried = existing.Entries
            .Where(entry => entry.FingerprintV2.Length > 0 && !seen.Contains(entry.FingerprintV2))
            .Select(entry => new Finding {
                    RuleId = entry.RuleId,
                    Severity = SkalaSeverity.Hidden,
                    Message = entry.Message,
                    Path = Path.Combine(report.RepositoryRoot, entry.Path),
                    EnclosingSymbol = string.Empty,
                    Snippet = string.Empty
                }
            );

        return [.. firing, .. carried];
    }

    static void Describe(StringBuilder builder, string verb, string path, int before, int firing) =>
        builder.Append("baseline ")
            .Append(verb)
            .Append("  ")
            .AppendLine(path)
            .Append("  ")
            .Append(before.ToString(CultureInfo.InvariantCulture))
            .Append(" accepted before  ·  ")
            .Append(firing.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" finding(s) firing now");

    static string Show(Baseline baseline, BaselineComparison comparison, RunReport report, string path) {
        var builder = new StringBuilder();
        builder.Append(path)
            .Append("  ·  ")
            .Append(baseline.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" accepted");
        builder.AppendLine();

        builder.Append("  new       ")
            .AppendLine(comparison.NewCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("  existing  ")
            .AppendLine((report.Findings.Length - comparison.NewCount).ToString(CultureInfo.InvariantCulture));
        builder.Append("  fixed     ")
            .AppendLine(comparison.Fixed.Length.ToString(CultureInfo.InvariantCulture));

        if (baseline.Count == 0) {
            builder.AppendLine();
            builder.AppendLine("  no baseline yet. `skala baseline create --apply` writes one.");
        }

        return builder.ToString();
    }
}
