using System.Text.Json;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>One rule as the catalogue and the id register jointly describe it.</summary>
public sealed record RuleRecord(string Id, string Concept, string DefaultSeverity, bool Retired, bool Allocated);

/// <summary>
///     The rule catalogue as a compatibility surface: ids, concepts and default severities.
/// </summary>
/// <remarks>
///     ⚠ Read out of <c>rules.json</c> and <c>allocated-ids.txt</c> rather than out of the analyzer
///     assembly, because those two files are what ADR-012 freezes and what a repository's
///     <c>.skala/baseline.sarif</c> is keyed on. A baseline is a set of <c>(rule, file, hash)</c>
///     tuples, so:
///     <list type="bullet">
///         <item>
///             a <b>removed or retired</b> id silently un-suppresses nothing and suppresses nothing, but every
///             entry naming it becomes dead weight the audit can no longer explain — <b>major</b>;
///         </item>
///         <item>
///             a <b>redefined concept</b> on an allocated id is worse: the baseline entries still match and now
///             suppress a different rule — <b>major</b>, and <c>RuleCatalogTests.RuleIds_AreAppendOnly</c>
///             should have failed the build long before the release saw it;
///         </item>
///         <item>
///             a <b>raised default severity</b> can fail a gate that passed, and can fail a build outright when
///             it crosses into <c>warning</c> under <c>TreatWarningsAsErrors</c> — <b>major</b>;
///         </item>
///         <item>
///             a <b>new rule at <c>warning</c> or above</b> can do the same to a repository that has no baseline
///             entry for it, which is every repository, because the entry cannot pre-exist the rule. It is
///             <b>minor</b> rather than major only because doc 11's <c>SkalaRulesAsErrors=false</c> default and
///             the baseline mechanism exist precisely for this and are the documented adoption path.
///         </item>
///     </list>
/// </remarks>
public static class RuleSurface {
    public const string Name = "rule catalogue";

    /// <summary>
    ///     ⚠ The order the catalogue uses, lowest first. Not <c>DiagnosticSeverity</c>'s and not
    ///     <c>SkalaSeverity</c>'s: the JSON carries ReSharper's five names, and <c>none</c> is a real
    ///     value that a rule can ship at.
    /// </summary>
    static readonly string[] Severities = ["none", "hint", "suggestion", "warning", "error"];

    public static DetectorResult Run(string? baselineRoot, string candidateRoot) {
        var candidate = Read(candidateRoot);

        if (baselineRoot is null) {
            return DetectorResult.Unmeasured(
                Name,
                $"no previous release — the {candidate.Count} rules in this one are the baseline"
            );
        }

        var baseline = Read(baselineRoot);
        var bump = BumpKind.Patch;
        var details = new List<string>();

        foreach (var (id, before) in baseline.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) {
            if (!candidate.TryGetValue(id, out var after)) {
                bump = BumpKind.Major;
                details.Add($"{id} ({before.Concept}) was **removed** — ADR-012 forbids deleting an allocated id");
                continue;
            }

            if (!string.Equals(before.Concept, after.Concept, StringComparison.Ordinal)) {
                bump = BumpKind.Major;
                details.Add($"{id} was **redefined**: `{before.Concept}` → `{after.Concept}`");
            }

            if (after.Retired && !before.Retired) {
                bump = BumpKind.Major;
                details.Add($"{id} ({after.Concept}) was **retired**");
            }

            var movement = Rank(after.DefaultSeverity) - Rank(before.DefaultSeverity);
            if (movement > 0) {
                bump = BumpKind.Major;
                details.Add($"{id} default severity **raised**: {before.DefaultSeverity} → {after.DefaultSeverity}");
            } else if (movement < 0) {
                details.Add($"{id} default severity lowered: {before.DefaultSeverity} → {after.DefaultSeverity}");
            }

            if (before.Allocated && !after.Allocated) {
                bump = BumpKind.Major;
                details.Add($"{id} left `allocated-ids.txt` — an allocated id is permanent (ADR-012)");
            }
        }

        var added = candidate.Values
            .Where(rule => !baseline.ContainsKey(rule.Id))
            .OrderBy(static rule => rule.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var rule in added) {
            if (Rank(rule.DefaultSeverity) >= Rank("warning")) {
                bump = Max(bump, BumpKind.Minor);
                details.Add(
                    $"{rule.Id} ({rule.Concept}) **added at {rule.DefaultSeverity}** — it can fail a "
                    + "`TreatWarningsAsErrors` build that has no baseline entry for it"
                );
            } else {
                details.Add($"{rule.Id} ({rule.Concept}) added at {rule.DefaultSeverity}");
            }
        }

        var retired = candidate.Values.Count(static rule => rule.Retired);
        var headline = details.Count == 0
            ? $"unchanged — {candidate.Count} rules, {retired} retired"
            : $"{added.Count} added, {details.Count - added.Count} other change(s) across {candidate.Count} rules";

        return DetectorResult.Measured(Name, bump, headline, details);
    }

    public static IReadOnlyDictionary<string, RuleRecord> Read(string repositoryRoot) {
        var metadata = Path.Combine(repositoryRoot, "Rules", "Rikarin.Skala.Rules.Metadata");
        var catalogue = Path.Combine(metadata, "rules.json");
        if (!File.Exists(catalogue)) {
            throw new FileNotFoundException($"No rule catalogue at '{catalogue}'.", catalogue);
        }

        var allocated = Allocated(Path.Combine(metadata, "allocated-ids.txt"));

        using var document = JsonDocument.Parse(File.ReadAllText(catalogue));
        var rules = new Dictionary<string, RuleRecord>(StringComparer.Ordinal);

        foreach (var rule in document.RootElement.GetProperty("rules").EnumerateArray()) {
            var id = rule.GetProperty("id").GetString()!;
            rules[id] = new(
                id,
                rule.TryGetProperty("concept", out var concept) ? concept.GetString() ?? "" : "",
                rule.TryGetProperty("defaultSeverity", out var severity) ? severity.GetString() ?? "" : "",
                rule.TryGetProperty("retired", out var retired) && retired.GetBoolean(),
                allocated.Contains(id)
            );
        }

        return rules;
    }

    static HashSet<string> Allocated(string path) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path)) {
            return ids;
        }

        foreach (var line in File.ReadLines(path)) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) {
                continue;
            }

            ids.Add(trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        }

        return ids;
    }

    static int Rank(string severity) {
        var index = Array.IndexOf(Severities, severity);

        // ⚠ An unknown name is not ranked 0. A severity this tool has never heard of, ranked below
        // `none`, would report every rule that carries it as a *lowering* and pass the release.
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                $"'{severity}' is not one of the catalogue's severities ({string.Join(", ", Severities)})."
            );
    }

    internal static BumpKind Max(BumpKind left, BumpKind right) => left > right ? left : right;
}
