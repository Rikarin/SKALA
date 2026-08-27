using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

public sealed record CommandResult(int ExitCode, string Output) {
    public static CommandResult Ok(string output) => new(0, output);
}

/// <summary>
/// The implementations behind <c>skala config</c>.
/// </summary>
/// <remarks>
/// ⚠ They live in Core, not in the CLI: nothing may reference <c>Rikarin.Skala.Cli</c>
/// (docs/plan/02 § "The project graph"), because MSBuild and MCP need to host the same logic. The
/// CLI is argument parsing and rendering only.
/// </remarks>
public static class ConfigCommands {
    /// <summary>Exit code for "the configuration has findings and --strict was asked for".</summary>
    public const int StrictFailure = 1;

    /// <summary>
    /// The effective option set for one file, each with its source file:line and its tier.
    /// </summary>
    /// <param name="configPath">
    /// A single <c>.editorconfig</c> to resolve against instead of the chain above
    /// <paramref name="sourcePath"/>. This is how the export is explained before it is installed.
    /// </param>
    public static CommandResult Explain(
        string sourcePath,
        string? repositoryRoot = null,
        bool configuredOnly = false,
        string? configPath = null
    ) {
        var resolution = configPath is null
            ? OptionResolver.Resolve(sourcePath)
            : ResolveStandalone(configPath, sourcePath);
        var diagnostics = ConfigurationAnalyzer.Analyze(resolution, repositoryRoot);
        var output = new StringBuilder();

        output.Append("Effective configuration for ").AppendLine(resolution.SourcePath);
        output.AppendLine();
        output.AppendLine(".editorconfig chain, outermost first:");
        foreach (var document in resolution.Chain.Documents) {
            output.Append("  ").Append(document.Path).AppendLine(document.IsRoot ? "  (root = true)" : string.Empty);
        }

        if (!resolution.Chain.StoppedAtRoot) {
            output.AppendLine("  ⚠ the walk reached the filesystem root without finding `root = true`");
        }

        output.AppendLine();
        var rows = (configuredOnly ? resolution.Configured : resolution.Resolved).ToArray();
        var keyWidth = rows.Length == 0 ? 3 : rows.Max(static option => option.Info.Key.Length);
        var valueWidth = Math.Min(
            28,
            rows.Length == 0 ? 5 : Math.Max(5, rows.Max(static option => option.Value.Length))
        );

        output.Append("option".PadRight(keyWidth))
            .Append("  ")
            .Append("value".PadRight(valueWidth))
            .Append("  tier  source")
            .AppendLine();
        output.AppendLine(new string('-', keyWidth + valueWidth + 40));

        foreach (var option in rows) {
            output.Append(option.Info.Key.PadRight(keyWidth))
                .Append("  ")
                .Append(Truncate(option.Value, valueWidth).PadRight(valueWidth))
                .Append("  ")
                .Append(option.Info.Tier.ToString().PadRight(4))
                .Append("  ")
                .AppendLine(option.SourceText);
        }

        output.AppendLine();
        output.AppendLine(Counts(resolution));
        AppendDiagnostics(output, diagnostics);
        return CommandResult.Ok(output.ToString());
    }

    /// <summary>The tier report and every configuration finding, for the repository's own config.</summary>
    /// <param name="target">
    /// A directory, in which case the whole <c>.editorconfig</c> chain above it is checked, or a
    /// single <c>.editorconfig</c>-shaped file, in which case only that file is.
    /// </param>
    public static CommandResult Check(string target, bool strict = false) {
        var full = Path.GetFullPath(target);
        var isFile = File.Exists(full) && !Directory.Exists(full);
        var directory = isFile ? Path.GetDirectoryName(full)! : full;
        var sourcePath = Path.Combine(directory, "Skala.cs");
        var resolution = isFile
            ? OptionResolver.Resolve(EditorConfigChain.Of(sourcePath, EditorConfigDocument.Load(full)))
            : OptionResolver.Resolve(sourcePath);
        var diagnostics = ConfigurationAnalyzer.Analyze(resolution, directory).ToBuilder();

        var toolConfig = ToolConfiguration.Find(directory);
        if (toolConfig is not null) {
            diagnostics.AddRange(toolConfig.Diagnostics);
        }

        var output = new StringBuilder();
        output.Append("Configuration check for ").AppendLine(full);
        output.AppendLine();
        output.AppendLine(Counts(resolution));
        output.AppendLine();

        var byNamespace = resolution.Unknown
            .GroupBy(static key => key.Namespace)
            .OrderBy(static group => group.Key.ToString(), StringComparer.Ordinal);
        output.AppendLine("Keys the option registry does not own:");
        foreach (var group in byNamespace) {
            output.Append("  ")
                .Append(group.Key.ToString().PadRight(20))
                .Append(group.Count().ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .AppendLine(
                    group.Key switch {
                        KeyNamespace.Option => "  reported as SK9001",
                        KeyNamespace.InspectionSeverity => "  ReSharper inspection severities — Milestone 5",
                        KeyNamespace.DiagnosticSeverity => "  Roslyn analyzer severities — Milestone 5",
                        KeyNamespace.NamingRule => "  Roslyn's own naming engine; Skala never reimplements it",
                        _ => string.Empty
                    }
                );
        }

        output.AppendLine();
        var nearest = resolution.Chain.Documents.LastOrDefault();
        if (nearest is not null && !nearest.IsRoot) {
            output.AppendLine(
                $"⚠ {nearest.Path} has no `root = true`. An .editorconfig above the repository still applies."
            );
            output.AppendLine("  `skala config fix` adds it.");
        }

        var hasStandardWidth =
            OptionRegistry.TryResolve("max_line_length", out var widthId) && !resolution[widthId].IsDefault;
        var reSharperWidth = OptionRegistry.TryResolve("resharper_csharp_max_line_length", out var rsWidthId)
            ? resolution[rsWidthId]
            : null;
        if (!hasStandardWidth && reSharperWidth is { IsDefault: false }) {
            output.AppendLine(
                $"⚠ no `max_line_length`; the column limit lives only in `resharper_csharp_max_line_length = {reSharperWidth.Value}`."
            );
            output.AppendLine(
                "  Every other tool in the ecosystem therefore does not know the width. `skala config fix` adds it."
            );
        }

        output.AppendLine();
        AppendDiagnostics(output, diagnostics.ToImmutable());

        var failing = diagnostics.Any(static d => d.Severity == SkalaSeverity.Error)
            || (strict && diagnostics.Any(static d => d.Severity >= SkalaSeverity.Warning));

        return new CommandResult(failing ? StrictFailure : 0, output.ToString());
    }

    /// <summary>What changes between two <c>.editorconfig</c> files, semantically rather than textually.</summary>
    public static CommandResult Diff(string left, string right) {
        var probe = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(left)) ?? ".", "Probe.cs");
        var before = ResolveStandalone(left, probe);
        var after = ResolveStandalone(right, probe);

        var output = new StringBuilder();
        output.Append("--- ").AppendLine(Path.GetFullPath(left));
        output.Append("+++ ").AppendLine(Path.GetFullPath(right));
        output.AppendLine();

        var changes = 0;
        for (var i = 0; i < OptionRegistry.Count; i++) {
            var id = (OptionId)i;
            var a = before[id];
            var b = after[id];
            if (string.Equals(a.Value, b.Value, StringComparison.Ordinal) && a.IsDefault == b.IsDefault) {
                continue;
            }

            changes++;
            var key = OptionRegistry.Get(id).Key;
            output.Append(key).Append(": ").Append(Describe(a)).Append(" -> ").AppendLine(Describe(b));
        }

        output.AppendLine();
        output.AppendLine(
            changes == 0
                ? "No semantic difference: the two files resolve to the same option set."
                : $"{changes.ToString(CultureInfo.InvariantCulture)} option(s) differ."
        );

        return CommandResult.Ok(output.ToString());
    }

    /// <summary>
    /// Writes back the subset of an export that differs from ReSharper's defaults.
    /// </summary>
    public static CommandResult Distill(string path, string? outputPath) {
        var document = EditorConfigDocument.Load(path);
        var result = Distiller.Distill(document);

        if (outputPath is not null) {
            File.WriteAllText(outputPath, result.Text);
        }

        var output = new StringBuilder();
        output.AppendLine(Distiller.Summary(result));
        output.AppendLine();
        output.AppendLine("What a key has to prove before it is dropped:");
        output.AppendLine("  JetBrains' EditorConfig property tables publish each property's name, language and");
        output.AppendLine("  possible values, and never its default — so no key can claim `resharper-docs` and, until");
        output.AppendLine("  milestone 3, distill dropped nothing at all. The defaults are derived from the oracle");
        output.AppendLine("  instead: a `jb cleanupcode` run under a configuration carrying nothing but `root = true`");
        output.AppendLine(
            "  is ReSharper-with-defaults by construction, and the value that reproduces it on the key's"
        );
        output.AppendLine("  own fixture is the default. Those are marked `oracle-probe` and only those are dropped.");
        output.AppendLine("  Every other key is kept, because dropping one on a guessed default would silently change");
        output.AppendLine("  formatting in whoever's repository accepted this file.");

        if (outputPath is null) {
            output.AppendLine();
            output.Append(result.Text);
        }

        return CommandResult.Ok(output.ToString());
    }

    /// <summary>Adds <c>root</c> and <c>max_line_length</c>, and optionally resolves contradictions.</summary>
    public static CommandResult Fix(string path, bool apply, bool resolveContradictions) {
        var document = EditorConfigDocument.Load(path);
        var result = Fixer.Fix(document, resolveContradictions);

        var output = new StringBuilder();
        if (!result.Changed) {
            return CommandResult.Ok("Nothing to fix." + Environment.NewLine);
        }

        output.Append(apply ? "Applied to " : "Would apply to ").Append(Path.GetFullPath(path)).AppendLine(":");
        foreach (var change in result.Applied) {
            output.Append("  - ").AppendLine(change);
        }

        if (apply) {
            File.WriteAllText(path, result.Text);
        } else {
            output.AppendLine();
            output.AppendLine("Re-run with --apply to write the file.");
        }

        return CommandResult.Ok(output.ToString());
    }

    public static ResolutionResult ResolveStandalone(string editorConfigPath, string sourcePath) {
        var document = EditorConfigDocument.Load(editorConfigPath);
        return OptionResolver.Resolve(EditorConfigChain.Of(sourcePath, document));
    }

    static string Describe(ResolvedOption option) => option.IsDefault ? $"(default) {option.Value}" : option.Value;

    static string Counts(ResolutionResult resolution) {
        var configured = resolution.Configured.Count();
        var tiers = resolution.Resolved.GroupBy(static option => option.Info.Tier)
            .ToDictionary(
                static g => g.Key,
                static g => g.Count()
            );

        string Tier(OptionTier tier) =>
            tiers.TryGetValue(tier, out var count) ? count.ToString(CultureInfo.InvariantCulture) : "0";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{OptionRegistry.Count} options known: {configured} set by the configuration, {OptionRegistry.Count - configured} at the registry default."
            + $"{Environment.NewLine}Tiers — A (implemented): {Tier(OptionTier.A)}, B (approximated): {Tier(OptionTier.B)}, C (accepted, ignored): {Tier(OptionTier.C)}, D (not implemented): {Tier(OptionTier.D)}."
        );
    }

    static void AppendDiagnostics(StringBuilder output, ImmutableArray<SkalaDiagnostic> diagnostics) {
        if (diagnostics.Length == 0) {
            output.AppendLine("No configuration diagnostics.");
            return;
        }

        var ordered = diagnostics
            .OrderByDescending(static d => d.Severity)
            .ThenBy(static d => d.Id, StringComparer.Ordinal)
            .ThenBy(static d => d.Line);

        output.AppendLine("Diagnostics:");
        var shown = 0;
        foreach (var diagnostic in ordered) {
            if (diagnostic.Id == ConfigDiagnosticIds.UnknownKey && shown >= 20) {
                continue;
            }

            output.Append("  ").AppendLine(diagnostic.ToString());
            if (diagnostic.Detail is not null) {
                output.Append("      ").AppendLine(diagnostic.Detail);
            }

            if (diagnostic.Id == ConfigDiagnosticIds.UnknownKey) {
                shown++;
            }
        }

        var unknown = diagnostics.Count(static d => d.Id == ConfigDiagnosticIds.UnknownKey);
        if (unknown > 20) {
            output.Append("  … and ")
                .Append((unknown - 20).ToString(CultureInfo.InvariantCulture))
                .AppendLine(" more SK9001. Unknown keys are info, not warnings, by design.");
        }

        output.AppendLine();
        foreach (var group in diagnostics.GroupBy(static d => d.Id)
                     .OrderBy(static g => g.Key, StringComparer.Ordinal)) {
            output.Append("  ")
                .Append(group.Key)
                .Append(": ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

    static string Truncate(string value, int width) =>
        value.Length <= width ? value : value[..Math.Max(1, width - 1)] + "…";
}
