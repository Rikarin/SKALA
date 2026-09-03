using Rikarin.Skala.Options;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Core.Configuration;

public sealed record FixResult(string Text, ImmutableArray<string> Applied) {
    public bool Changed => Applied.Length > 0;
}

/// <summary>
///     The in-place repairs <c>skala config fix</c> offers for the two hazards the export ships with:
///     no <c>root = true</c>, and a column limit only ReSharper can see.
/// </summary>
/// <remarks>docs/plan/03-configuration-model.md § "Four things about that file that will bite".</remarks>
public static class Fixer {
    public static FixResult Fix(EditorConfigDocument document, bool resolveContradictions = false) {
        var applied = ImmutableArray.CreateBuilder<string>();
        var newline = document.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = document.Text.Split('\n').Select(static line => line.TrimEnd('\r')).ToList();

        var insertions = new Dictionary<int, List<string>>();
        var replacements = new Dictionary<int, string>();

        if (!document.IsRoot) {
            insertions[1] = ["root = true", string.Empty];
            applied.Add(
                "added `root = true`, so the walk stops here instead of picking up an .editorconfig above the repository"
            );
        }

        var width = document.Assignments.FirstOrDefault(static a => a.Key == "skala_max_line_length");
        var standardWidth = document.Assignments.FirstOrDefault(static a => a.Key == "max_line_length");
        if (width is not null && standardWidth is null) {
            if (!insertions.TryGetValue(width.Line, out var list)) {
                insertions[width.Line] = list = [];
            }

            list.Add($"max_line_length = {width.Value}");
            applied.Add(
                $"added `max_line_length = {width.Value}` beside the ReSharper key, so every other tool can see the column limit too"
            );
        }

        if (resolveContradictions) {
            foreach (var rule in ConfigurationAnalyzer.KnownContradictions) {
                var generic = document.Assignments.FirstOrDefault(a => a.Key == rule.Generic);
                var specific = document.Assignments.FirstOrDefault(a => a.Key == rule.Specific);
                if (generic is null || specific is null || !rule.Conflicts(generic.Value, specific.Value)) {
                    continue;
                }

                var value = Agreeing(rule.Generic, specific.Value);
                if (value is null) {
                    continue;
                }

                replacements[generic.Line] = $"{rule.Generic} = {value}";
                applied.Add(
                    $"set `{rule.Generic} = {value}` to agree with `{rule.Specific} = {specific.Value}`, which already wins"
                );
            }

            foreach (var option in OptionRegistry.All) {
                var byKey = document.Assignments.Where(a => OptionRegistry.TryResolve(a.Key, out var id)
                    && id == option.Id
                )
                    .ToArray();
                if (byKey.Length < 2) {
                    continue;
                }

                var winner = byKey.MinBy(static a => OptionResolver.SpecificityOf(a.Key))!;
                foreach (var loser in byKey) {
                    if (ReferenceEquals(loser, winner)
                        || string.Equals(
                            loser.Value,
                            winner.Value,
                            StringComparison.Ordinal
                        )) {
                        continue;
                    }

                    replacements[loser.Line] = $"{loser.Key} = {winner.Value}";
                    applied.Add(
                        $"set `{loser.Key} = {winner.Value}` to agree with `{winner.Key}`, the more specific spelling, which already wins"
                    );
                }
            }
        }

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++) {
            var number = i + 1;
            if (insertions.TryGetValue(number, out var before)) {
                foreach (var line in before) {
                    builder.Append(line).Append(newline);
                }
            }

            builder.Append(replacements.TryGetValue(number, out var replacement) ? replacement : lines[i]);
            if (i < lines.Count - 1 || lines[i].Length > 0) {
                builder.Append(newline);
            }
        }

        return new(builder.ToString(), applied.ToImmutable());
    }

    /// <summary>The value the losing generic key would need to stop contradicting the winner.</summary>
    static string? Agreeing(string genericKey, string specificValue) =>
        genericKey switch {
            "trim_trailing_whitespace" => specificValue is "true" or "always" ? "true" : "false",
            "max_line_length" => specificValue,
            // end_of_line names a line ending and skala_enforce_line_ending_style is a switch, so
            // there is no value of end_of_line that agrees with `false`. The fix is to turn enforcement
            // on, which is a style decision, and `fix` does not make those.
            _ => null
        };
}
