using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Sarif;
using Newtonsoft.Json;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     One accepted finding, as the baseline stores it.
/// </summary>
/// <remarks>
///     ⚠ The fingerprints are the identity; the rule id, path and message are carried so that the
///     baseline's <em>diff</em> is readable. docs/plan/09: the file is "a reviewed, committed
///     artefact — its diff in a PR is 'we suppressed these', which is exactly the conversation that
///     should happen", and a diff of opaque hashes is not that conversation.
/// </remarks>
public sealed record BaselineEntry(
    string RuleId,
    string Path,
    string Message,
    string FingerprintV2,
    string FingerprintV1);

/// <summary>
///     <c>.skala/baseline.sarif</c> — the findings the repository has accepted for now.
/// </summary>
/// <remarks>
///     docs/plan/09 § "The baseline". A normal SARIF file, so every tool that reads SARIF can read it
///     and nothing new has to be invented or documented.
///     <para>
///         ⚠ Matching is on <see cref="Fingerprints.Version2" />, falling back to
///         <see cref="Fingerprints.Version1" /> for an entry written before the fingerprint gained its last
///         two terms. The fallback is one-directional: a v2 baseline is never matched by a v1 hash alone,
///         because v1 is the weaker identity and letting it match would silently widen what the baseline
///         suppresses.
///     </para>
/// </remarks>
public sealed class Baseline {
    Baseline(ImmutableArray<BaselineEntry> entries, string path) {
        Entries = entries;
        Path = path;
        _v2 = entries.Select(static entry => entry.FingerprintV2)
            .Where(static value => value.Length > 0)
            .ToImmutableHashSet(StringComparer.Ordinal);
        _v1 = entries.Where(static entry => entry.FingerprintV2.Length == 0)
            .Select(static entry => entry.FingerprintV1)
            .Where(static value => value.Length > 0)
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    readonly ImmutableHashSet<string> _v2;
    readonly ImmutableHashSet<string> _v1;

    public ImmutableArray<BaselineEntry> Entries { get; }

    public string Path { get; }

    public int Count => Entries.Length;

    /// <summary>The conventional location, relative to the repository root.</summary>
    public const string DefaultRelativePath = ".skala/baseline.sarif";

    public static string DefaultPath(string repositoryRoot) =>
        System.IO.Path.Combine(repositoryRoot, ".skala", "baseline.sarif");

    public static Baseline Empty(string path) => new([], path);

    /// <summary>
    ///     Reads a baseline, or returns an empty one when the file is absent.
    /// </summary>
    /// <remarks>
    ///     ⚠ An absent file is empty; an unreadable one throws. The difference matters: "there is no
    ///     baseline yet" is an ordinary state on a repository that has not run <c>baseline create</c>,
    ///     and "the baseline is corrupt" must not be silently treated as "nothing was accepted", which
    ///     would turn every existing finding new and fail the gate for a reason nothing names.
    /// </remarks>
    public static Baseline Read(string path) {
        if (!File.Exists(path)) {
            return Empty(path);
        }

        var log = JsonConvert.DeserializeObject<SarifLog>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is not a SARIF log.");

        var entries = ImmutableArray.CreateBuilder<BaselineEntry>();
        foreach (var run in log.Runs ?? []) {
            foreach (var result in run.Results ?? []) {
                var prints = result.PartialFingerprints;
                entries.Add(
                    new BaselineEntry(
                        result.RuleId ?? string.Empty,
                        Location(result),
                        result.Message?.Text ?? string.Empty,
                        Print(prints, Fingerprints.Version2),
                        Print(prints, Fingerprints.Version1)
                    )
                );
            }
        }

        return new Baseline(entries.ToImmutable(), path);
    }

    static string Print(IDictionary<string, string>? prints, string key) =>
        prints is not null && prints.TryGetValue(key, out var value) ? value : string.Empty;

    static string Location(Result result) =>
        result.Locations is [{ PhysicalLocation.ArtifactLocation.Uri: { } uri }, ..]
            ? uri.ToString()
            : string.Empty;

    /// <summary>Whether the baseline already accepted this finding.</summary>
    public bool Contains(Finding finding) =>
        _v2.Contains(Fingerprints.V2(finding)) || _v1.Contains(Fingerprints.V1(finding));

    /// <summary>
    ///     Splits a run against this baseline.
    /// </summary>
    /// <remarks>
    ///     docs/plan/09's three buckets. ⚠ <b>Fixed</b> is the one worth having and the one a naive
    ///     implementation drops: a finding the baseline holds that no longer fires is good news, and it
    ///     is also the only signal that a rule has silently stopped working. Reporting it is what makes
    ///     pruning a decision rather than a side effect.
    /// </remarks>
    public BaselineComparison Compare(IEnumerable<Finding> findings) {
        var partitioned = ImmutableArray.CreateBuilder<Finding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var newCount = 0;

        foreach (var finding in findings) {
            var v2 = Fingerprints.V2(finding);
            var known = _v2.Contains(v2) || _v1.Contains(Fingerprints.V1(finding));
            if (known) {
                seen.Add(v2);
                seen.Add(Fingerprints.V1(finding));
            } else {
                newCount++;
            }

            partitioned.Add(finding with { Bucket = known ? BaselineBucket.Existing : BaselineBucket.New });
        }

        var fixedEntries = ImmutableArray.CreateBuilder<BaselineEntry>();
        foreach (var entry in Entries) {
            var identity = entry.FingerprintV2.Length > 0 ? entry.FingerprintV2 : entry.FingerprintV1;
            if (identity.Length > 0 && !seen.Contains(identity)) {
                fixedEntries.Add(entry);
            }
        }

        return new BaselineComparison(partitioned.ToImmutable(), newCount, fixedEntries.ToImmutable());
    }

    /// <summary>
    ///     Writes a baseline holding exactly the findings given.
    /// </summary>
    /// <remarks>
    ///     ⚠ Suppressed findings are written too. A baseline exists to record what the repository
    ///     accepted, and a <c>#pragma</c> is a second, less visible way of accepting something; leaving
    ///     them out means removing the pragma turns an accepted finding new.
    ///     <para>
    ///         ⚠ The bucket is cleared, for the same reason the invocation is. A baseline is a list of
    ///         what the repository accepts, not the record of a comparison — and since M9 an
    ///         <see cref="BaselineBucket.Existing" /> finding carries a <c>suppressions</c> entry saying
    ///         "the baseline accepted this", which inside the baseline file itself would be the file
    ///         citing itself.
    ///     </para>
    /// </remarks>
    public static void Write(string path, RunReport report, IEnumerable<Finding> findings) {
        var accepted = findings.Select(static finding => finding with { Bucket = BaselineBucket.Unknown })
            .ToArray();

        var log = SarifWriter.Build(report with { Findings = [.. accepted], Gate = null });
        log.Runs[0].Invocations = null;

        Core.SkalaDirectory.EnsureForFile(System.IO.Path.GetFullPath(path));
        File.WriteAllText(path, SarifWriter.Serialize(log));
    }
}

/// <summary>The result of holding a run up against a baseline.</summary>
/// <param name="Findings">Every finding, each tagged with the bucket it fell into.</param>
/// <param name="NewCount">How many were not in the baseline. What <c>newIssues</c> gates on.</param>
/// <param name="Fixed">
///     ⚠ Baseline entries that no longer fire. Reported as good news and pruned only when asked —
///     docs/plan/09: "a baseline that self-prunes lets a rule that silently stopped working look like
///     progress".
/// </param>
public sealed record BaselineComparison(
    ImmutableArray<Finding> Findings,
    int NewCount,
    ImmutableArray<BaselineEntry> Fixed);
