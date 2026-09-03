using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using System.Collections.Immutable;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>One option the local block deliberately takes back from the canonical.</summary>
public sealed record LocalOverride(string Key, string Section, string CanonicalValue, string LocalValue, int Line);

/// <summary>Which way a <c>dotnet_diagnostic</c> severity moves when the canonical is applied.</summary>
public enum SeverityMove {
    /// <summary>
    ///     The file did not set it and the canonical does. ⚠ The dangerous one — see
    ///     <see cref="DiagnosticSeverityChange" />.
    /// </summary>
    Introduced,

    /// <summary>Both set it, and the canonical's is louder.</summary>
    Raised,

    /// <summary>Both set it, and the canonical's is quieter.</summary>
    Lowered,

    /// <summary>The file set it and the canonical does not, so the key stops being set.</summary>
    Dropped
}

/// <summary>
///     One compiler or analyzer diagnostic whose severity changes when the canonical is applied.
/// </summary>
/// <remarks>
///     ⚠ <b>This exists because adopting the canonical turned a green build red and nothing said so.</b>
///     The canonical is a Rider export and the export carries 213
///     <c>dotnet_diagnostic.cs*.severity</c> lines. The first repository to adopt it carried none of
///     them; one of the 213 raises <c>CS9209</c> above the compiler's own default, and with
///     <c>TreatWarningsAsErrors</c> — which is not exotic — the <c>.editorconfig</c> commit *alone*,
///     touching no code, took a tree from <b>0 errors to 17, in 15 files</b>. It was isolated by
///     rebuilding with only that file swapped.
///     <para>
///         Nothing reported it. <see cref="LocalOverride" /> and its <c>SK9013</c> cover keys the option
///         registry owns, and <c>dotnet_diagnostic</c> keys are deliberately not in that registry;
///         <c>config check</c> files them under "keys the option registry does not own" and moves on. So
///         the loudest thing the canonical does to a repository was the one thing it did silently.
///     </para>
///     <para>
///         ⚠ <b>The report is the fix, not a change to the payload.</b> The severities are what the
///         canonical is for. What was missing is that adopting one must state which compiler diagnostics it
///         moves and in which direction, <em>before</em> it is applied.
///     </para>
/// </remarks>
/// <param name="Before">Null when the file being replaced does not set the key at all.</param>
/// <param name="After">Null when the key stops being set.</param>
public sealed record DiagnosticSeverityChange(
    string Key,
    string Diagnostic,
    string Section,
    string? Before,
    string? After,
    SeverityMove Move) {
    /// <summary>
    ///     A compiler diagnostic — <c>CS….</c> for C#, <c>BC….</c> for Visual Basic — rather than an
    ///     analyzer's.
    /// </summary>
    /// <remarks>
    ///     ⚠ The distinction is the whole point. An analyzer severity that goes up adds findings; a
    ///     <em>compiler</em> severity that goes up under <c>TreatWarningsAsErrors</c> stops the build,
    ///     and the person reading the diff sees only an `.editorconfig` change.
    /// </remarks>
    /// <summary>
    ///     A C# compiler diagnostic. ⚠ Listed before the Visual Basic ones everywhere, because the
    ///     canonical is a Rider export and carries both: sorting the 253 by id alone put 23 <c>BC….</c>
    ///     ids ahead of every <c>CS….</c> one, so a capped list showed a C# repository nothing but
    ///     Visual Basic.
    /// </summary>
    public bool IsCSharp => IsCompilerDiagnostic && Diagnostic.StartsWith("CS", StringComparison.OrdinalIgnoreCase);

    public bool IsCompilerDiagnostic =>
        (Diagnostic.StartsWith("CS", StringComparison.OrdinalIgnoreCase)
            || Diagnostic.StartsWith("BC", StringComparison.OrdinalIgnoreCase))
        && Diagnostic.Length > 2
        && Diagnostic.AsSpan(2).ContainsAnyExceptInRange('0', '9') is false;

    /// <summary>Louder than it was, or newly set at a level that can fail a build.</summary>
    public bool CanBreakABuild =>
        Move is SeverityMove.Introduced or SeverityMove.Raised
        && After is "warning" or "error";
}

/// <summary>Where a repository stands relative to the canonical.</summary>
public sealed record CanonicalStatus(
    string Path,
    bool Exists,
    CanonicalLayoutResult Layout,
    string ActualSha,
    CanonicalManifest Tool,
    ImmutableArray<LocalOverride> Overrides,
    ImmutableArray<string> LocalSections,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    ImmutableArray<DiagnosticSeverityChange> SeverityChanges) {
    /// <summary>The subset that can turn a build that compiles into one that does not.</summary>
    public IEnumerable<DiagnosticSeverityChange> BuildBreaking =>
        SeverityChanges.Where(static change => change.IsCompilerDiagnostic && change.CanBreakABuild);

    /// <summary>The managed block no longer hashes to what its marker claims.</summary>
    public bool Drifted =>
        Layout.IsManaged && !string.Equals(Layout.Marker!.Sha256, ActualSha, StringComparison.OrdinalIgnoreCase);

    /// <summary>The marker matches the block, but a newer canonical exists. Never a failure.</summary>
    public bool Behind =>
        Layout.IsManaged && !Drifted && !string.Equals(ActualSha, Tool.Sha256, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nothing to do: managed, intact, and on the canonical this build carries.</summary>
    public bool Current => Layout.IsManaged && !Drifted && !Behind;
}

/// <summary>What <c>skala config sync</c> would write, and what it changed to get there.</summary>
public sealed record SyncResult(string Path, string Text, ImmutableArray<string> Applied, CanonicalStatus Before) {
    public bool Changed => Applied.Length > 0;
}

/// <summary>
///     The canonical half of <c>skala config</c>: where a repository stands, and how it is brought back.
/// </summary>
/// <remarks>
///     ⚠ ⚠ The distribution mechanism is a <b>command</b>, not a restore hook, and the reason is
///     measured rather than assumed. See docs/plan/03 § "Canonical distribution across repositories":
///     NuGet copies neither <c>content/</c> nor <c>contentFiles/</c> into a consuming project directory
///     under <c>PackageReference</c>, and a package's <c>build/*.targets</c> are not imported during
///     restore at all — so "drops it at restore time" cannot be built. Dropping it from a build target
///     can, and is worse: measured on a probe repository, the config took <b>three</b> builds to become
///     effective, of which the first two passed green.
/// </remarks>
public static class CanonicalSync {
    public static CanonicalStatus Status(string target) =>
        Status(target, CanonicalEditorConfig.Manifest, CanonicalEditorConfig.Text);

    public static CanonicalStatus Status(string target, CanonicalManifest tool, string toolPayload) {
        var path = ResolvePath(target);
        var exists = File.Exists(path);
        var text = exists ? File.ReadAllText(path) : string.Empty;
        return Describe(path, exists, text, tool, toolPayload);
    }

    /// <summary>The status of a file the caller already has. Everything else is filesystem access.</summary>
    public static CanonicalStatus Describe(
        string path,
        bool exists,
        string text,
        CanonicalManifest tool,
        string toolPayload
    ) {
        var layout = CanonicalLayout.Split(text);
        var actual = layout.IsManaged ? CanonicalEditorConfig.Hash(layout.CanonicalText) : string.Empty;
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var canonicalDocument = EditorConfigDocument.FromText(
            path,
            layout.IsManaged ? layout.CanonicalText : toolPayload
        );
        var localDocument = EditorConfigDocument.FromText(path, layout.LocalText);

        var overrides = Overrides(canonicalDocument, localDocument, layout.LocalFirstLine);
        var sections = localDocument.Sections
            .Where(static section => section.Name is not null)
            .Select(static section => section.Name!)
            .ToImmutableArray();

        if (!exists) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalUnmanaged,
                    SkalaSeverity.Info,
                    "the repository has no .editorconfig; `skala config sync --apply` writes the canonical one",
                    path
                )
            );
        } else if (!layout.IsManaged) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalUnmanaged,
                    SkalaSeverity.Info,
                    ".editorconfig carries no canonical block, so drift from the canonical cannot be detected",
                    path,
                    1,
                    "`skala config sync --apply` adopts it: the canonical goes on top, everything already in the file is preserved verbatim below the `skala:local begin` marker."
                )
            );
        }

        var severityChanges = SeverityChangesFor(path, text, toolPayload, layout);
        var status = new CanonicalStatus(path, exists, layout, actual, tool, overrides, sections, [], severityChanges);

        if (status.Drifted) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalDrift,
                    SkalaSeverity.Error,
                    $"the canonical block has been edited: it hashes to {Short(actual)} and its marker says {Short(layout.Marker!.Sha256)}",
                    path,
                    1,
                    "Move the edit below `# skala:local begin`, where it survives every canonical bump, then `skala config sync --apply` to restore the block."
                )
            );
        }

        if (status.Behind) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalBehind,
                    SkalaSeverity.Info,
                    $"this repository is on canonical {layout.Marker!.Version}; {tool.Version} is available",
                    path,
                    1,
                    "Informational by design. Bumping is a reformatting commit, and eighteen repositories must not have to make it on the same day. `skala config diff --canonical --options` prices the bump."
                )
            );
        }

        foreach (var local in overrides) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalLocalOverride,
                    SkalaSeverity.Info,
                    $"[{local.Section}] {local.Key} = {local.LocalValue} overrides the canonical's {local.CanonicalValue}",
                    path,
                    local.Line
                )
            );
        }

        AppendSeverityDiagnostics(diagnostics, status, path);

        return status with { Diagnostics = diagnostics.ToImmutable() };
    }

    /// <summary>
    ///     Replace the canonical block and leave everything below the local marker exactly as it was.
    /// </summary>
    public static SyncResult Sync(string target) =>
        Sync(target, CanonicalEditorConfig.Manifest, CanonicalEditorConfig.Text);

    public static SyncResult Sync(string target, CanonicalManifest tool, string toolPayload) {
        var path = ResolvePath(target);
        var exists = File.Exists(path);
        return SyncText(path, exists, exists ? File.ReadAllText(path) : string.Empty, tool, toolPayload);
    }

    /// <summary>The whole of sync, over text the caller already has.</summary>
    public static SyncResult SyncText(
        string path,
        bool exists,
        string text,
        CanonicalManifest tool,
        string toolPayload
    ) {
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
            applied.Add(
                $"adopted the file: canonical {tool.Version} on top, the existing {CanonicalLayout.Number(before.LocalSections.Length)} section(s) preserved verbatim below `{CanonicalLayout.LocalMarker}`"
            );
        } else if (before.Drifted) {
            applied.Add(
                $"restored the canonical block, which had been edited away from its marker ({Short(before.Layout.Marker!.Sha256)})"
            );
        } else if (before.Behind) {
            applied.Add($"moved the canonical block from {before.Layout.Marker!.Version} to {tool.Version}");
        }

        if (applied.Count == 0
            && !string.Equals(CanonicalEditorConfig.Normalize(text), assembled, StringComparison.Ordinal)) {
            applied.Add("normalised the markers and the local banner");
        }

        return new(before.Path, assembled, applied.ToImmutable(), before);
    }

    /// <summary>
    ///     Which registry options the local block takes back from the canonical.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Compared by exact spelling within a section, falling back to the canonical's <c>[*]</c> —
    ///         which is what a reader of the two blocks would do, and what the local section is in practice
    ///         narrowing. ⚠ The tempting shortcut, "the canonical's last value for this
    ///         <see cref="OptionId" />", is wrong twice over: it conflates a key with its aliases (so
    ///         <c>skala_insert_final_newline = false</c> reads as an override of
    ///         <c>skala_insert_final_newline = true</c>, which is the export's own contradiction
    ///         and already <c>SK9005</c>) and it conflates sections (so <c>[*.csv]</c> reads as overriding
    ///         <c>[*]</c>). Both fired on Skala's own configuration.
    ///     </para>
    ///     <para>
    ///         Restricted to keys the registry owns. The 3 021 inspection severities and the 215 naming keys
    ///         are Milestone 5's business, and reporting them would bury the handful that matter — Vixen
    ///         alone would produce some two hundred lines of per-file <c>dotnet_diagnostic</c> suppressions.
    ///     </para>
    /// </remarks>
    static ImmutableArray<LocalOverride> Overrides(
        EditorConfigDocument canonical,
        EditorConfigDocument local,
        int localFirstLine
    ) {
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

            result.Add(
                new LocalOverride(
                    assignment.Key,
                    sectionName,
                    canonicalValue,
                    assignment.Value,
                    localFirstLine + assignment.Line - 1
                )
            );
        }

        return result.ToImmutable();
    }

    /// <summary>
    ///     The <c>SK9016</c> half of the status: what applying the canonical does to the severities the
    ///     compiler and the analyzers are run at.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Summaries, not 213 lines.</b> A repository that sets none of the canonical's severities
    ///     gets one diagnostic per direction rather than one per key — the detail belongs in
    ///     <c>config diff --canonical</c>, which is the command whose job is pricing the change. A
    ///     report that buries its own headline under two hundred lines is a report nobody reads to the
    ///     end, and the headline here is a number and a mechanism.
    ///     <para>
    ///         ⚠ The build-breaking summary is a <b>warning</b>, and it is the only thing in this file that
    ///         is. Drift is an error because somebody edited a managed block; being behind is info because
    ///         eighteen repositories must not go red on a publication day. This one sits between them: it
    ///         is not wrong, and it will stop your build.
    ///     </para>
    /// </remarks>
    static void AppendSeverityDiagnostics(
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CanonicalStatus status,
        string path
    ) {
        var breaking = status.BuildBreaking.ToArray();
        if (breaking.Length > 0) {
            var introduced = breaking.Count(static change => change.Move == SeverityMove.Introduced);
            var raised = breaking.Length - introduced;

            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalSeverityChange,
                    SkalaSeverity.Warning,
                    $"the canonical moves {CanonicalLayout.Number(breaking.Length)} compiler diagnostic severity(ies) "
                    + $"up to warning or error ({CanonicalLayout.Number(introduced)} this file does not set at all, "
                    + $"{CanonicalLayout.Number(raised)} raised): "
                    + string.Join(
                        ", ",
                        breaking.OrderByDescending(static change => change.IsCSharp)
                            .ThenBy(static change => change.Diagnostic, StringComparer.OrdinalIgnoreCase)
                            .Take(6)
                            .Select(static change => change.Diagnostic)
                    )
                    + (breaking.Length > 6 ? ", …" : string.Empty),
                    path,
                    1,
                    "⚠ With `TreatWarningsAsErrors` these become build errors, from an .editorconfig commit "
                    + "that touches no code — this took one repository from 0 errors to 17. "
                    + "`skala config diff --canonical` lists every one before you apply it."
                )
            );
        }

        var quieter = status.SeverityChanges
            .Count(static change =>
                change.Move is SeverityMove.Lowered or SeverityMove.Dropped && change.IsCompilerDiagnostic
            );

        if (quieter > 0) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalSeverityChange,
                    SkalaSeverity.Info,
                    $"the canonical lowers or stops setting {CanonicalLayout.Number(quieter)} compiler diagnostic severity(ies)",
                    path,
                    1,
                    "A severity turned down is the widest kind of suppression there is (docs/plan/09 § "
                    + "\"--no-new-suppressions\"). `skala config diff --canonical` lists them."
                )
            );
        }

        var analyzers = status.SeverityChanges.Count(static change => !change.IsCompilerDiagnostic);
        if (analyzers > 0) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalSeverityChange,
                    SkalaSeverity.Info,
                    $"the canonical also changes {CanonicalLayout.Number(analyzers)} analyzer diagnostic severity(ies)",
                    path,
                    1,
                    "These add or remove findings rather than breaking the build."
                )
            );
        }
    }

    /// <summary>
    ///     Which <c>dotnet_diagnostic.&lt;id&gt;.severity</c> keys move when the canonical is applied to
    ///     this file, and in which direction.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠ <b>Effective value against effective value, not block against block.</b> The comparison
    ///         that matters is what the compiler ends up being told, so "before" is the whole file as it
    ///         stands and "after" is the incoming canonical with the local block laid over it — because
    ///         sync preserves the local block verbatim <em>below</em> the canonical one, and editorconfig
    ///         resolves later sections over earlier ones. A block-against-block comparison would report a
    ///         key the local block already pins as changing, which is the one case where nothing changes.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>An introduction has no measurable direction and is reported as its own kind.</b> When
    ///         the file sets nothing, the severity it is moving away from is the compiler's own default for
    ///         that diagnostic, which is not written down anywhere in an `.editorconfig` and differs by
    ///         language version. Inventing a table of Roslyn defaults to make the arrow point somewhere
    ///         would be a second copy of somebody else's data, wrong on the next compiler release, so the
    ///         report says what it actually knows — "this file does not set it; the canonical sets it to
    ///         <c>warning</c>" — and names <c>TreatWarningsAsErrors</c>, which is the mechanism that turns
    ///         that into a build failure.
    ///     </para>
    /// </remarks>
    internal static ImmutableArray<DiagnosticSeverityChange> SeverityChangesFor(
        string path,
        string currentText,
        string toolPayload,
        CanonicalLayoutResult layout
    ) {
        var before = Severities(path, currentText);
        var after = Severities(path, toolPayload);

        // The local block survives sync untouched and comes last, so it wins over the canonical.
        foreach (var (key, value) in Severities(path, layout.LocalText)) {
            after[key] = value;
        }

        var result = ImmutableArray.CreateBuilder<DiagnosticSeverityChange>();
        foreach (var key in before.Keys.Concat(after.Keys)
                     .Distinct()
                     .OrderBy(static entry => entry.Section, StringComparer.Ordinal)
                     .ThenBy(static entry => entry.Diagnostic, StringComparer.OrdinalIgnoreCase)) {
            var had = before.TryGetValue(key, out var was);
            var has = after.TryGetValue(key, out var now);

            if (had && has && string.Equals(was, now, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var move = (had, has) switch {
                (false, true) => SeverityMove.Introduced,
                (true, false) => SeverityMove.Dropped,
                _ => Rank(now) > Rank(was) ? SeverityMove.Raised : SeverityMove.Lowered
            };

            result.Add(
                new DiagnosticSeverityChange(
                    $"dotnet_diagnostic.{key.Diagnostic.ToLowerInvariant()}.severity",
                    key.Diagnostic,
                    key.Section,
                    had ? was : null,
                    has ? now : null,
                    move
                )
            );
        }

        return result.ToImmutable();
    }

    /// <summary>
    ///     Every <c>dotnet_diagnostic.&lt;id&gt;.severity</c> in a document, last assignment winning.
    /// </summary>
    /// <remarks>
    ///     ⚠ Keyed by section as well as id, because a severity under <c>[Tools/**/*.cs]</c> and the
    ///     same severity under <c>[*]</c> are different settings — docs/plan/09 § "--no-new-suppressions"
    ///     makes the section header part of a severity's identity for exactly this reason.
    /// </remarks>
    static Dictionary<(string Section, string Diagnostic), string> Severities(string path, string text) {
        var result = new Dictionary<(string, string), string>();
        if (text.Trim().Length == 0) {
            return result;
        }

        foreach (var assignment in EditorConfigDocument.FromText(path, text).Assignments) {
            if (assignment.Section.Name is not { } section
                || OptionResolver.Classify(assignment.Key) != KeyNamespace.DiagnosticSeverity) {
                continue;
            }

            // `dotnet_diagnostic.<id>.severity` — the middle segment is the id, and `Classify`
            // has already established the shape.
            var body = assignment.Key["dotnet_diagnostic.".Length..];
            var dot = body.LastIndexOf('.');
            if (dot <= 0) {
                continue;
            }

            // ⚠ Upper-cased for the report. `EditorConfigDocument` lower-cases every key, because
            // editorconfig keys are case-insensitive — but `cs9209` in a message about a build
            // failure is not something a reader can search the build log for, and `CS9209` is.
            result[(section, body[..dot].ToUpperInvariant())] = assignment.Value;
        }

        return result;
    }

    /// <summary>
    ///     editorconfig's severity ladder. ⚠ -1 for anything unrecognised, so it never outranks a real level.
    /// </summary>
    static int Rank(string? severity) =>
        severity?.ToLowerInvariant() switch {
            "none" => 0,
            "silent" => 1,
            "suggestion" => 2,
            "warning" => 3,
            "error" => 4,
            _ => -1
        };

    static string ResolvePath(string target) {
        var full = Path.GetFullPath(target);
        return Directory.Exists(full) ? Path.Combine(full, EditorConfigDocument.FileName) : full;
    }

    static string Short(string sha) => sha.Length <= 12 ? sha : sha[..12];
}
