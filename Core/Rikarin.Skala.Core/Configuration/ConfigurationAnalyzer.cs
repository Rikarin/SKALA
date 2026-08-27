using System.Collections.Immutable;
using System.Globalization;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
/// Two settings that mean opposite things and are not two spellings of one option, so precedence
/// cannot be read off the key names alone.
/// </summary>
/// <param name="Generic">The standard EditorConfig key, which loses.</param>
/// <param name="Specific">The ReSharper key, which wins for C#.</param>
/// <param name="Conflicts">True when the pair's current values actually disagree.</param>
public sealed record ContradictionRule(
    string Generic,
    string Specific,
    Func<string, string, bool> Conflicts,
    string Explanation);

/// <summary>
/// SK9001–SK9006 over a resolved configuration.
/// </summary>
/// <remarks>
/// Non-negotiable #4 (docs/plan/00): unknown configuration is a diagnostic, never a silent default.
/// The failure mode this prevents is the config saying one thing, the tool doing another, and
/// nothing ever telling you.
/// </remarks>
public static class ConfigurationAnalyzer {
    /// <summary>
    /// The contradictions the plan already found in the export, plus the width one. These are not
    /// alias disagreements — the two keys are different options that happen to describe the same
    /// behaviour — so they cannot be detected structurally and are listed.
    /// docs/plan/03-configuration-model.md § "Four things about that file that will bite".
    /// </summary>
    public static ImmutableArray<ContradictionRule> KnownContradictions { get; } = [
        new(
            "trim_trailing_whitespace",
            "resharper_remove_spaces_on_blank_lines",
            static (generic, specific) => IsFalse(generic) && IsTrue(specific),
            "trim_trailing_whitespace says leave trailing whitespace alone; resharper_remove_spaces_on_blank_lines says strip it from blank lines."
        ),
        new(
            "end_of_line",
            "resharper_enforce_line_ending_style",
            static (generic, specific) => generic.Length > 0 && IsFalse(specific),
            "end_of_line names a line ending; resharper_enforce_line_ending_style says do not enforce one."
        ),
        new(
            "max_line_length",
            "resharper_csharp_max_line_length",
            static (generic, specific) => generic.Length > 0
                && specific.Length > 0
                && !string.Equals(
                    generic,
                    specific,
                    StringComparison.Ordinal
                ),
            "Two column limits that disagree. Skala reads the ReSharper key as authoritative."
        )
    ];

    public static ImmutableArray<SkalaDiagnostic> Analyze(ResolutionResult resolution, string? repositoryRoot = null) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        AddUnknownKeys(diagnostics, resolution);
        AddInheritedFromAbove(diagnostics, resolution, repositoryRoot);
        AddDuplicateAliases(diagnostics, resolution);
        AddContradictions(diagnostics, resolution);
        AddUnhonourableSettings(diagnostics, resolution);
        return diagnostics.ToImmutable();
    }

    static void AddUnknownKeys(ImmutableArray<SkalaDiagnostic>.Builder diagnostics, ResolutionResult resolution) {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unknown in resolution.Unknown) {
            if (unknown.Namespace != KeyNamespace.Option || !reported.Add(unknown.Assignment.Key)) {
                continue;
            }

            var suggestion = DidYouMean(unknown.Assignment.Key);
            var message = suggestion is null
                ? $"'{unknown.Assignment.Key}' is not an option Skala knows"
                : $"'{unknown.Assignment.Key}' is not an option Skala knows; did you mean '{suggestion}'?";

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.UnknownKey,
                    SkalaSeverity.Info,
                    message,
                    unknown.Assignment.File,
                    unknown.Assignment.Line
                )
            );
        }
    }

    static void AddInheritedFromAbove(
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        ResolutionResult resolution,
        string? repositoryRoot
    ) {
        if (repositoryRoot is null) {
            return;
        }

        foreach (var document in resolution.Chain.Above(repositoryRoot)) {
            var keys = resolution.Configured
                .Where(option => option.Origin is not null
                    && string.Equals(
                        option.Origin.File,
                        document.Path,
                        StringComparison.Ordinal
                    )
                )
                .Select(static option => option.Origin!.Spelling)
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToArray();

            var detail = keys.Length == 0
                ? "no option in the effective set came from it"
                : $"{keys.Length.ToString(CultureInfo.InvariantCulture)} option(s) came from it: {string.Join(", ", keys.Take(8))}{(keys.Length > 8 ? ", …" : string.Empty)}";

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.InheritedFromAbove,
                    SkalaSeverity.Info,
                    $"the effective configuration draws from '{document.Path}', which is above the repository root",
                    document.Path,
                    0,
                    detail
                )
            );
        }
    }

    static void AddDuplicateAliases(ImmutableArray<SkalaDiagnostic>.Builder diagnostics, ResolutionResult resolution) {
        foreach (var option in resolution.Configured) {
            var winner = option.Origin!;
            foreach (var candidate in option.Candidates) {
                if (ReferenceEquals(candidate, winner)
                    || candidate.Specificity != winner.Specificity
                    || string.Equals(candidate.Spelling, winner.Spelling, StringComparison.Ordinal)
                    || string.Equals(candidate.Value, winner.Value, StringComparison.Ordinal)) {
                    continue;
                }

                diagnostics.Add(
                    new SkalaDiagnostic(
                        ConfigDiagnosticIds.DuplicateAlias,
                        SkalaSeverity.Warning,
                        $"'{candidate.Spelling}' and '{winner.Spelling}' are two spellings of the same option, are equally specific, and disagree ('{candidate.Value}' vs '{winner.Value}')",
                        candidate.File,
                        candidate.Line,
                        "Precedence cannot choose between them. Delete one."
                    )
                );
            }
        }
    }

    static void AddContradictions(ImmutableArray<SkalaDiagnostic>.Builder diagnostics, ResolutionResult resolution) {
        // Two spellings of one option that disagree. This is the insert_final_newline case: the
        // generic key and the C# key are the same option, and the C# key wins.
        foreach (var option in resolution.Configured) {
            var winner = option.Origin!;
            foreach (var candidate in option.Candidates) {
                if (candidate.Specificity <= winner.Specificity
                    || string.Equals(
                        candidate.Value,
                        winner.Value,
                        StringComparison.Ordinal
                    )) {
                    continue;
                }

                diagnostics.Add(
                    new SkalaDiagnostic(
                        ConfigDiagnosticIds.ContradictoryOptions,
                        SkalaSeverity.Warning,
                        $"'{candidate.Spelling} = {candidate.Value}' contradicts '{winner.Spelling} = {winner.Value}'; the C# key wins, so the effective value is '{winner.Value}'",
                        candidate.File,
                        candidate.Line,
                        $"ReSharper resolves this by language specificity. Winner: {winner.File}:{winner.Line.ToString(CultureInfo.InvariantCulture)}."
                    )
                );
            }
        }

        // Pairs that describe the same behaviour under different names.
        foreach (var rule in KnownContradictions) {
            if (!TryFind(resolution, rule.Generic, out var generic)
                || !TryFind(
                    resolution,
                    rule.Specific,
                    out var specific
                )) {
                continue;
            }

            if (!rule.Conflicts(generic.Value, specific.Value)) {
                continue;
            }

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.ContradictoryOptions,
                    SkalaSeverity.Warning,
                    $"'{generic.Origin!.Spelling} = {generic.Value}' contradicts '{specific.Origin!.Spelling} = {specific.Value}'; the C# key wins, so the effective behaviour is '{specific.Origin.Spelling} = {specific.Value}'",
                    generic.Origin.File,
                    generic.Origin.Line,
                    rule.Explanation
                )
            );
        }
    }

    static void AddUnhonourableSettings(
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        ResolutionResult resolution
    ) {
        // docs/plan/16 § Q1: indentation autodetection makes the IDE and the oracle disagree with
        // each other, and Skala — which has no autodetection — cannot match both.
        foreach (var key in new[] {
                "resharper_autodetect_indent_settings", "resharper_apply_auto_detected_rules",
                "resharper_use_indent_from_vs"
            }) {
            if (!TryFind(resolution, key, out var option) || !IsTrue(option.Value)) {
                continue;
            }

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.UnhonourableSetting,
                    SkalaSeverity.Warning,
                    $"'{option.Origin!.Spelling} = {option.Value}' is a setting Skala cannot honour",
                    option.Origin.File,
                    option.Origin.Line,
                    "Skala has no indentation autodetection to switch off, so the IDE would format against a detected indent and Skala against the configured one. docs/plan/16 § Q1."
                )
            );
        }

        foreach (var key in new[] { "resharper_old_engine", "resharper_use_old_engine" }) {
            if (!TryFind(resolution, key, out var option) || !IsTrue(option.Value)) {
                continue;
            }

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.UnhonourableSetting,
                    SkalaSeverity.Warning,
                    $"'{option.Origin!.Spelling} = {option.Value}' selects ReSharper's previous formatting engine, which Skala does not reproduce",
                    option.Origin.File,
                    option.Origin.Line
                )
            );
        }
    }

    static bool TryFind(ResolutionResult resolution, string key, out ResolvedOption option) {
        if (OptionRegistry.TryResolve(key, out var id)) {
            option = resolution[id];
            return !option.IsDefault;
        }

        option = null!;
        return false;
    }

    static bool IsTrue(string value) => value is "true" or "always";

    static bool IsFalse(string value) => value is "false" or "never";

    /// <summary>The nearest registry spelling by edit distance, when one is near enough to be a typo.</summary>
    public static string? DidYouMean(string key) {
        var best = (string?)null;
        var bestDistance = int.MaxValue;
        var limit = Math.Max(2, key.Length / 4);

        foreach (var spelling in OptionRegistry.Spellings) {
            if (Math.Abs(spelling.Length - key.Length) > limit) {
                continue;
            }

            var distance = Distance(key, spelling, limit);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = spelling;
            }
        }

        return bestDistance <= limit ? best : null;
    }

    static int Distance(string a, string b, int limit) {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++) {
            current[0] = i;
            var rowBest = current[0];
            for (var j = 1; j <= b.Length; j++) {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowBest = Math.Min(rowBest, current[j]);
            }

            if (rowBest > limit) {
                return int.MaxValue;
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
