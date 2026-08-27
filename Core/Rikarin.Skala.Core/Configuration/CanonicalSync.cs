using System.Collections.Immutable;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>One option the local block deliberately takes back from the canonical.</summary>
public sealed record LocalOverride(string Key, string Section, string CanonicalValue, string LocalValue, int Line);

/// <summary>Where a repository stands relative to the canonical.</summary>
public sealed record CanonicalStatus(
    string Path,
    bool Exists,
    CanonicalLayoutResult Layout,
    string ActualSha,
    CanonicalManifest Tool,
    ImmutableArray<LocalOverride> Overrides,
    ImmutableArray<string> LocalSections,
    ImmutableArray<SkalaDiagnostic> Diagnostics) {
    /// <summary>The managed block no longer hashes to what its marker claims.</summary>
    public bool Drifted => Layout.IsManaged && !string.Equals(Layout.Marker!.Sha256, ActualSha, StringComparison.OrdinalIgnoreCase);

    /// <summary>The marker matches the block, but a newer canonical exists. Never a failure.</summary>
    public bool Behind => Layout.IsManaged && !Drifted && !string.Equals(ActualSha, Tool.Sha256, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nothing to do: managed, intact, and on the canonical this build carries.</summary>
    public bool Current => Layout.IsManaged && !Drifted && !Behind;
}

/// <summary>What <c>skala config sync</c> would write, and what it changed to get there.</summary>
public sealed record SyncResult(string Path, string Text, ImmutableArray<string> Applied, CanonicalStatus Before) {
    public bool Changed => Applied.Length > 0;
}

/// <summary>
/// The canonical half of <c>skala config</c>: where a repository stands, and how it is brought back.
/// </summary>
/// <remarks>
/// ⚠ ⚠ The distribution mechanism is a <b>command</b>, not a restore hook, and the reason is
/// measured rather than assumed. See docs/plan/03 § "Canonical distribution across repositories":
/// NuGet copies neither <c>content/</c> nor <c>contentFiles/</c> into a consuming project directory
/// under <c>PackageReference</c>, and a package's <c>build/*.targets</c> are not imported during
/// restore at all — so "drops it at restore time" cannot be built. Dropping it from a build target
/// can, and is worse: measured on a probe repository, the config took <b>three</b> builds to become
/// effective, of which the first two passed green.
/// </remarks>
public static class CanonicalSync {
    public static CanonicalStatus Status(string target) => Status(target, CanonicalEditorConfig.Manifest, CanonicalEditorConfig.Text);

    public static CanonicalStatus Status(string target, CanonicalManifest tool, string toolPayload) {
        var path = ResolvePath(target);
        var exists = File.Exists(path);
        var text = exists ? File.ReadAllText(path) : string.Empty;
        return Describe(path, exists, text, tool, toolPayload);
    }

    /// <summary>The status of a file the caller already has. Everything else is filesystem access.</summary>
    public static CanonicalStatus Describe(string path, bool exists, string text, CanonicalManifest tool, string toolPayload) {
        var layout = CanonicalLayout.Split(text);
        var actual = layout.IsManaged ? CanonicalEditorConfig.Hash(layout.CanonicalText) : string.Empty;
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var canonicalDocument = EditorConfigDocument.FromText(
            path,
            layout.IsManaged ? layout.CanonicalText : toolPayload);
        var localDocument = EditorConfigDocument.FromText(path, layout.LocalText);

        var overrides = Overrides(canonicalDocument, localDocument, layout.LocalFirstLine);
        var sections = localDocument.Sections
            .Where(static section => section.Name is not null)
            .Select(static section => section.Name!)
            .ToImmutableArray();

        if (!exists) {
            diagnostics.Add(new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalUnmanaged,
                SkalaSeverity.Info,
                "the repository has no .editorconfig; `skala config sync --apply` writes the canonical one",
                path));
        } else if (!layout.IsManaged) {
            diagnostics.Add(new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalUnmanaged,
                SkalaSeverity.Info,
                ".editorconfig carries no canonical block, so drift from the canonical cannot be detected",
                path,
                1,
                "`skala config sync --apply` adopts it: the canonical goes on top, everything already in the file is preserved verbatim below the `skala:local begin` marker."));
        }

        var status = new CanonicalStatus(path, exists, layout, actual, tool, overrides, sections, []);

        if (status.Drifted) {
            diagnostics.Add(new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalDrift,
                SkalaSeverity.Error,
                $"the canonical block has been edited: it hashes to {Short(actual)} and its marker says {Short(layout.Marker!.Sha256)}",
                path,
                1,
                "Move the edit below `# skala:local begin`, where it survives every canonical bump, then `skala config sync --apply` to restore the block."));
        }

        if (status.Behind) {
            diagnostics.Add(new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalBehind,
                SkalaSeverity.Info,
                $"this repository is on canonical {layout.Marker!.Version}; {tool.Version} is available",
                path,
                1,
                "Informational by design. Bumping is a reformatting commit, and eighteen repositories must not have to make it on the same day. `skala config diff --canonical --options` prices the bump."));
        }

        foreach (var local in overrides) {
            diagnostics.Add(new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalLocalOverride,
                SkalaSeverity.Info,
                $"[{local.Section}] {local.Key} = {local.LocalValue} overrides the canonical's {local.CanonicalValue}",
                path,
                local.Line));
        }

        return status with { Diagnostics = diagnostics.ToImmutable() };
    }

    /// <summary>
    /// Replace the canonical block and leave everything below the local marker exactly as it was.
    /// </summary>
    public static SyncResult Sync(string target) => Sync(target, CanonicalEditorConfig.Manifest, CanonicalEditorConfig.Text);

    public static SyncResult Sync(string target, CanonicalManifest tool, string toolPayload) {
        var path = ResolvePath(target);
        var exists = File.Exists(path);
        return SyncText(path, exists, exists ? File.ReadAllText(path) : string.Empty, tool, toolPayload);
    }

    /// <summary>The whole of sync, over text the caller already has.</summary>
    public static SyncResult SyncText(string path, bool exists, string text, CanonicalManifest tool, string toolPayload) {
        var before = Describe(path, exists, text, tool, toolPayload);
        var applied = ImmutableArray.CreateBuilder<string>();

        var local = before.Layout.LocalText;
        if (!before.Layout.IsManaged && local.Trim().Length > 0) {
            local = CanonicalLayout.StripRoot(local, out var stripped);
            if (stripped) {
                applied.Add("moved `root = true` into the canonical block, which carries it in its own preamble");
            }
        }

        var assembled = CanonicalLayout.Assemble(toolPayload, tool.Version, local);

        if (!before.Exists) {
            applied.Add($"wrote a new .editorconfig carrying canonical {tool.Version}");
        } else if (!before.Layout.IsManaged) {
            applied.Add($"adopted the file: canonical {tool.Version} on top, the existing {CanonicalLayout.Number(before.LocalSections.Length)} section(s) preserved verbatim below `{CanonicalLayout.LocalMarker}`");
        } else if (before.Drifted) {
            applied.Add($"restored the canonical block, which had been edited away from its marker ({Short(before.Layout.Marker!.Sha256)})");
        } else if (before.Behind) {
            applied.Add($"moved the canonical block from {before.Layout.Marker!.Version} to {tool.Version}");
        }

        if (applied.Count == 0 && !string.Equals(CanonicalEditorConfig.Normalize(text), assembled, StringComparison.Ordinal)) {
            applied.Add("normalised the markers and the local banner");
        }

        return new SyncResult(before.Path, assembled, applied.ToImmutable(), before);
    }

    /// <summary>
    /// Which registry options the local block takes back from the canonical.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compared by exact spelling within a section, falling back to the canonical's <c>[*]</c> —
    /// which is what a reader of the two blocks would do, and what the local section is in practice
    /// narrowing. ⚠ The tempting shortcut, "the canonical's last value for this
    /// <see cref="OptionId"/>", is wrong twice over: it conflates a key with its aliases (so
    /// <c>insert_final_newline = false</c> reads as an override of
    /// <c>resharper_csharp_insert_final_newline = true</c>, which is the export's own contradiction
    /// and already <c>SK9005</c>) and it conflates sections (so <c>[*.csv]</c> reads as overriding
    /// <c>[*]</c>). Both fired on Skala's own configuration.
    /// </para>
    /// <para>
    /// Restricted to keys the registry owns. The 3 021 inspection severities and the 215 naming keys
    /// are Milestone 5's business, and reporting them would bury the handful that matter — Vixen
    /// alone would produce some two hundred lines of per-file <c>dotnet_diagnostic</c> suppressions.
    /// </para>
    /// </remarks>
    static ImmutableArray<LocalOverride> Overrides(EditorConfigDocument canonical, EditorConfigDocument local, int localFirstLine) {
        var bySection = new Dictionary<(string Section, string Key), string>();
        foreach (var section in canonical.Sections.Where(static section => section.Name is not null)) {
            foreach (var assignment in section.Assignments) {
                bySection[(section.Name!, assignment.Key)] = assignment.Value;
            }
        }

        var result = ImmutableArray.CreateBuilder<LocalOverride>();
        foreach (var assignment in local.Assignments) {
            if (assignment.Section.Name is not { } sectionName || !OptionRegistry.TryResolve(assignment.Key, out _)) {
                continue;
            }

            if (!bySection.TryGetValue((sectionName, assignment.Key), out var canonicalValue)
                && !bySection.TryGetValue(("*", assignment.Key), out canonicalValue)) {
                continue;
            }

            if (string.Equals(canonicalValue, assignment.Value, StringComparison.Ordinal)) {
                continue;
            }

            result.Add(new LocalOverride(
                assignment.Key,
                sectionName,
                canonicalValue,
                assignment.Value,
                localFirstLine + assignment.Line - 1));
        }

        return result.ToImmutable();
    }

    static string ResolvePath(string target) {
        var full = Path.GetFullPath(target);
        return Directory.Exists(full) ? Path.Combine(full, EditorConfigDocument.FileName) : full;
    }

    static string Short(string sha) => sha.Length <= 12 ? sha : sha[..12];
}
