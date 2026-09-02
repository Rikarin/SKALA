using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Release;

/// <summary>
///     The release notes, written from the measurements.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/18 § "The notes are the deliverable". Not generated from the commit log: doc 02
///     § "Repository policy" requires a formatting change to be listed with a corpus diff summary,
///     because downstream that change <b>is</b> a commit in someone's repository, and a commit message
///     cannot say how many of 716 files move. Four times in this project's history a summary and a
///     measurement disagreed and the measurement was right. So every line below is a number some
///     detector produced, and a surface that was not measured says so instead of saying "no change".
///     <para>
///         ⚠ The <c>CHANGELOG.md</c> block keeps the format that file already has —
///         <c>
/// ## &lt;version&gt; —
///  &lt;date&gt;
///         </c> with <c>### Added/Changed/Fixed</c> beneath — because that file was written by
///         hand from the merge history and a generator that reformatted it would make the whole record
///         unreadable in one commit.
///     </para>
/// </remarks>
public static class ReleaseNotes {
    public static string Render(ReleaseVerdict verdict, ReleaseRequest request, DateTimeOffset date) {
        var notes = new StringBuilder();

        notes.Append("# Skala ")
            .Append(verdict.Next)
            .Append(" — ")
            .AppendLine(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        notes.AppendLine();

        notes.Append(
            verdict.Previous is null
                ? "**The first release.** There is no predecessor to measure against, so every detector below "
                + "reports *unmeasured* rather than *unchanged* — the two produce the same version number and "
                + "must not produce the same sentence. What this release establishes is the baseline the next "
                + "one is measured against."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Measured against **{verdict.Previous}**. The verdict is **{verdict.Bump.ToString().ToLowerInvariant()}**; the version is the highest of the five surfaces below, not a summary of the commits."
                )
        )
            .AppendLine();
        notes.AppendLine();

        if (verdict.Previous is not null) {
            notes.AppendLine("| | |");
            notes.AppendLine("|---|---|");
            notes.Append("| previous | `").Append(verdict.Previous).AppendLine("` |");
            notes.Append("| this release | `").Append(verdict.Next).AppendLine("` |");
            notes.Append("| verdict | **").Append(verdict.Bump.ToString().ToLowerInvariant()).AppendLine("** |");
            if (request.Commit.Length > 0) {
                notes.Append("| commit | `").Append(request.Commit).AppendLine("` |");
            }

            // ⚠ Both fingerprints, in the notes, because "the two tools were different builds" is the
            // one claim the output detector rests on and a reader should be able to check it.
            notes.Append("| baseline tool | `").Append(Short(verdict.BaselineFingerprint)).AppendLine("` |");
            notes.Append("| candidate tool | `").Append(Short(verdict.CandidateFingerprint)).AppendLine("` |");
            notes.AppendLine();
        }

        notes.AppendLine("## What was measured");
        notes.AppendLine();
        notes.AppendLine("| Surface | Verdict | Measurement |");
        notes.AppendLine("|---|---|---|");

        foreach (var surface in verdict.Surfaces) {
            notes.Append("| ")
                .Append(surface.Surface)
                .Append(" | ")
                .Append(
                    surface.State == DetectorState.Measured
                        ? surface.Bump == BumpKind.Patch
                            ? "—"
                            : "**" + surface.Bump.ToString().ToLowerInvariant() + "**"
                        : "*unmeasured*"
                )
                .Append(" | ")
                .Append(surface.Headline.Replace("|", """\|""", StringComparison.Ordinal))
                .AppendLine(" |");
        }

        notes.AppendLine();

        foreach (var surface in verdict.Surfaces.Where(static surface => surface.Details.Count > 0)) {
            notes.Append("### ").AppendLine(surface.Surface);
            notes.AppendLine();
            notes.AppendLine(surface.Headline + ".");
            notes.AppendLine();

            // ⚠ Truncated with the count of what was dropped rather than silently. A list that says
            // "12 of 340" is a measurement; a list of 12 with no denominator is a sample.
            foreach (var detail in surface.Details.Take(25)) {
                notes.Append("- ").AppendLine(detail);
            }

            if (surface.Details.Count > 25) {
                notes.Append("- …and ")
                    .Append((surface.Details.Count - 25).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" more.");
            }

            notes.AppendLine();
        }

        if (verdict.Output is { ChangedFiles: > 0 } output) {
            notes.AppendLine("### What a repository will see");
            notes.AppendLine();
            notes.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Taking this release reformats **{output.ChangedFiles} of {output.Comparable}** comparable corpus files, {output.ChangedLines} lines in total. Downstream that is a commit in your repository ([02](docs/plan/02-repository-layout.md) § \"Repository policy\"), so take it in its own commit, and pin the version in a local tool manifest ([11](docs/plan/11-cli-and-integrations.md) § \"Distribution\") so that two developers do not format the same tree two ways."
                )
            )
                .AppendLine();
            notes.AppendLine();
        }

        if (!verdict.AnySurfaceMeasured && verdict.Previous is not null) {
            notes.AppendLine(
                "⚠ **No surface was measured.** A baseline was named but nothing could be compared; "
                + "this version number is a guess, not a measurement."
            );
            notes.AppendLine();
        }

        notes.AppendLine("---");
        notes.AppendLine();
        notes.AppendLine("## The `CHANGELOG.md` entry");
        notes.AppendLine();
        notes.AppendLine("```markdown");
        notes.Append(Changelog(verdict, date));
        notes.AppendLine("```");

        return notes.ToString();
    }

    /// <summary>The block to paste into <c>CHANGELOG.md</c>, in the format that file already uses.</summary>
    public static string Changelog(ReleaseVerdict verdict, DateTimeOffset date) {
        var entry = new StringBuilder();
        entry.Append("## ")
            .Append(verdict.Next)
            .Append(" — ")
            .AppendLine(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        entry.AppendLine();

        if (verdict.Output is { ChangedFiles: > 0 } output) {
            entry.AppendLine("### Changed — formatting output");
            entry.AppendLine();
            entry.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"⚠ **{output.ChangedFiles} of {output.Comparable} corpus files format differently** than under `{verdict.Previous}` — {output.ChangedLines} lines. The classes, largest first:"
                )
            )
                .AppendLine();
            entry.AppendLine();

            foreach (var (name, lines, files, _) in output.Classes.Take(10)) {
                entry.Append("- ")
                    .Append(lines.ToString(CultureInfo.InvariantCulture))
                    .Append(" lines across ")
                    .Append(files.ToString(CultureInfo.InvariantCulture))
                    .Append(" files — ")
                    .AppendLine(name);
            }

            entry.AppendLine();
        }

        foreach (var surface in verdict.Surfaces
                     .Where(static surface => surface.State == DetectorState.Measured
                         && surface.Details.Count > 0
                         && surface.Surface != Surfaces.OutputSurface.Name
                     )) {
            entry.Append("### Changed — ").AppendLine(surface.Surface);
            entry.AppendLine();
            foreach (var detail in surface.Details.Take(15)) {
                entry.Append("- ").AppendLine(detail);
            }

            if (surface.Details.Count > 15) {
                entry.Append("- …and ")
                    .Append((surface.Details.Count - 15).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" more.");
            }

            entry.AppendLine();
        }

        return entry.ToString();
    }

    static string Short(string fingerprint) => fingerprint.Length >= 12 ? fingerprint[..12] : "(none)";
}
