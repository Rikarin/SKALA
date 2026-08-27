using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// docs/plan/03-configuration-model.md § "Canonical distribution across repositories" (Q4).
/// </summary>
public sealed class CanonicalDistributionTests {
    static CanonicalManifest Tool => CanonicalEditorConfig.Manifest;

    static string Payload => CanonicalEditorConfig.Text;

    // ── The payload, and the ADR-001 workflow that produces it ────────────────────────────────

    [Fact]
    public void TheCanonical_IsTheRiderExport_PlusExactlyTheTwoFixes() {
        // ⚠ ADR-001: the canonical *is* the export. If this fails, somebody re-exported from Rider
        // and did not run `./build.sh Canonical`, and eighteen repositories are about to be given
        // a configuration that is not the one in the IDE.
        var composed = CanonicalEditorConfig.Compose(File.ReadAllText(RepositoryPaths.Template));
        Assert.Equal(
            composed,
            File.ReadAllText(RepositoryPaths.CanonicalPayload).Replace("\r\n", "\n", StringComparison.Ordinal)
        );

        var export = EditorConfigDocument.Load(RepositoryPaths.Template);
        var canonical = EditorConfigDocument.FromText("canonical.editorconfig", composed);

        // Two keys added; every one of the export's own 4 226 assignments still present, verbatim.
        Assert.Equal(export.Assignments.Count() + 2, canonical.Assignments.Count());
        foreach (var assignment in export.Assignments) {
            Assert.Contains(canonical.Assignments, a => a.Key == assignment.Key && a.Value == assignment.Value);
        }

        Assert.True(canonical.IsRoot);
        Assert.Equal("120", Assert.Single(canonical.Assignments, static a => a.Key == "max_line_length").Value);
    }

    [Fact]
    public void TheEmbeddedPayload_IsTheOneTheDistributionPackageShips() {
        // One file on disk, two carriers. A payload that could differ between the tool and the
        // package is a payload with two versions.
        Assert.Equal(
            File.ReadAllText(RepositoryPaths.CanonicalPayload).Replace("\r\n", "\n", StringComparison.Ordinal),
            Payload
        );

        Assert.Equal(CanonicalEditorConfig.Hash(Payload), Tool.Sha256);
        Assert.Equal(4228, Tool.Assignments);
    }

    [Fact]
    public void ThePackagedTargets_AreLoadableXml() {
        // ⚠ Not paranoia: the first version of these files did not load, because an XML comment
        // cannot contain a double hyphen and the prose spelled out `--canonical`. Nothing in
        // `dotnet pack` checks this, so the failure surfaces in a consuming repository's build with
        // an MSB4024 that names Skala. It is cheaper to fail here.
        foreach (var targets in Directory.EnumerateFiles(
                     RepositoryPaths.CanonicalDirectory,
                     "*.targets",
                     SearchOption.AllDirectories
                 )) {
            System.Xml.Linq.XDocument.Load(targets);
        }
    }

    // ── Why the file has to be in the repository at all ───────────────────────────────────────

    [Fact]
    public void ACanonicalLeftInThePackageDirectory_ConfiguresNothingInTheRepository() {
        // ⚠ This is the fact that kills every "point the compiler at the packaged file" design.
        // editorconfig section globs resolve relative to the directory containing the file, so a
        // canonical sitting in ~/.nuget/packages/… has a `[*]` that matches only files under the
        // NuGet cache. Roslyn's own matcher is asked, so this is the compiler's answer, not ours.
        var repository = TempRoot;
        var inPackage = EditorConfigDocument.FromText(
            Path.Combine(Path.GetTempPath(), "nuget", "rikarin.skala.canonical", "0.1.0", "content", ".editorconfig"),
            Payload
        );
        var inRepository = EditorConfigDocument.FromText(Path.Combine(repository, ".editorconfig"), Payload);

        var source = Path.Combine(repository, "Core", "Thing.cs");
        Assert.Empty(SectionMatcher.CompilerView([inPackage], source));
        Assert.NotEmpty(SectionMatcher.CompilerView([inRepository], source));
    }

    // ── Sync ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sync_OnARepositoryWithNoEditorConfig_WritesAManagedFile() {
        var result = CanonicalSync.SyncText(
            Path.Combine(TempRoot, ".editorconfig"),
            false,
            string.Empty,
            Tool,
            Payload
        );

        Assert.True(result.Changed);
        var status = CanonicalSync.Describe(result.Path, true, result.Text, Tool, Payload);
        Assert.True(status.Current);
        Assert.Equal(Tool.Version, status.Layout.Marker!.Version);
        Assert.Equal(Tool.Sha256, status.ActualSha);
    }

    [Fact]
    public void Sync_IsIdempotent() {
        var path = Path.Combine(TempRoot, ".editorconfig");
        var once = CanonicalSync.SyncText(path, false, string.Empty, Tool, Payload).Text;
        var twice = CanonicalSync.SyncText(path, true, once, Tool, Payload);

        Assert.False(twice.Changed);
        Assert.Equal(once, twice.Text);
    }

    [Fact]
    public void Sync_MovesRootOutOfTheAdoptedText_AndSaysSo() {
        // The canonical carries `root = true` in its own preamble. A second one below a section
        // header is inert, and a line that looks load-bearing and is not is worse than no line.
        var result = CanonicalSync.SyncText(
            Path.Combine(TempRoot, ".editorconfig"),
            true,
            "root = true\n\n[*.cs]\nindent_size = 2\n",
            Tool,
            Payload
        );

        Assert.Contains(result.Applied, static change => change.Contains("root = true", StringComparison.Ordinal));
        Assert.DoesNotContain("[*.cs]\nroot", result.Text, StringComparison.Ordinal);

        var layout = CanonicalLayout.Split(result.Text);
        Assert.DoesNotContain("root", layout.LocalText, StringComparison.Ordinal);
        Assert.Contains("indent_size = 2", layout.LocalText, StringComparison.Ordinal);
    }

    // ── Drift, and the gate condition ─────────────────────────────────────────────────────────

    [Fact]
    public void EditingTheManagedBlock_IsDriftAndIsAnError() {
        var synced = CanonicalSync.SyncText(
            Path.Combine(TempRoot, ".editorconfig"),
            false,
            string.Empty,
            Tool,
            Payload
        ).Text;
        var tampered = synced.Replace("indent_size = 4", "indent_size = 2", StringComparison.Ordinal);

        var status = CanonicalSync.Describe(Path.Combine(TempRoot, ".editorconfig"), true, tampered, Tool, Payload);

        Assert.True(status.Drifted);
        Assert.False(status.Current);
        var drift = Assert.Single(status.Diagnostics, static d => d.Id == ConfigDiagnosticIds.CanonicalDrift);
        Assert.Equal(SkalaSeverity.Error, drift.Severity);
    }

    [Fact]
    public void EditingTheLocalBlock_IsNotDrift() {
        // The whole point. A repository must be able to say something about itself without the
        // gate calling it drift.
        var synced = CanonicalSync.SyncText(
            Path.Combine(TempRoot, ".editorconfig"),
            false,
            string.Empty,
            Tool,
            Payload
        ).Text;
        var withLocal = synced + "\n[*.generated.cs]\nindent_size = 2\n";

        var status = CanonicalSync.Describe(Path.Combine(TempRoot, ".editorconfig"), true, withLocal, Tool, Payload);

        Assert.False(status.Drifted);
        Assert.True(status.Current);
        Assert.DoesNotContain(status.Diagnostics, static d => d.Severity >= SkalaSeverity.Warning);
    }

    [Fact]
    public void ARepositoryOnAnOlderCanonical_IsBehind_NotDrifted() {
        // ⚠ Q4's third requirement: publishing a new canonical must not turn eighteen repositories
        // red on the same day. Being behind is information; only an edit is a finding.
        var older = Payload.Replace("indent_size = 4", "indent_size = 8", StringComparison.Ordinal);
        var olderManifest = CanonicalEditorConfig.DescribeManifest("0.0.9", older);
        var repository = CanonicalSync.SyncText(
            Path.Combine(TempRoot, ".editorconfig"),
            false,
            string.Empty,
            olderManifest,
            older
        ).Text;

        var status = CanonicalSync.Describe(Path.Combine(TempRoot, ".editorconfig"), true, repository, Tool, Payload);

        Assert.False(status.Drifted);
        Assert.True(status.Behind);
        Assert.Equal("0.0.9", status.Layout.Marker!.Version);
        Assert.DoesNotContain(status.Diagnostics, static d => d.Severity >= SkalaSeverity.Warning);
        Assert.Single(status.Diagnostics, static d => d.Id == ConfigDiagnosticIds.CanonicalBehind);
    }

    [Fact]
    public void ABumpIsPricedBeforeItIsTaken() {
        // `config diff --canonical --options` on a behind repository must name what changes, or
        // the per-repository bump is a leap rather than a decision.
        var older = Payload.Replace("indent_size = 4", "indent_size = 8", StringComparison.Ordinal);
        var olderManifest = CanonicalEditorConfig.DescribeManifest("0.0.9", older);
        var directory = TempRoot;
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, ".editorconfig"),
            CanonicalSync.SyncText(
                Path.Combine(directory, ".editorconfig"),
                false,
                string.Empty,
                olderManifest,
                older
            ).Text
        );

        var result = ConfigCommands.DiffCanonical(directory, showOptions: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CLEAN, behind", result.Output, StringComparison.Ordinal);
        Assert.Contains("indent_size: 8 -> 4", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffCanonical_ExitsWithTheConfigurationCodeOnDrift() {
        var directory = TempRoot;
        Directory.CreateDirectory(directory);
        var synced = CanonicalSync.SyncText(
            Path.Combine(directory, ".editorconfig"),
            false,
            string.Empty,
            Tool,
            Payload
        ).Text;
        File.WriteAllText(
            Path.Combine(directory, ".editorconfig"),
            synced.Replace("indent_size = 4", "indent_size = 2", StringComparison.Ordinal)
        );

        var result = ConfigCommands.DiffCanonical(directory);

        Assert.Equal(ConfigCommands.ConfigurationFailure, result.ExitCode);
        Assert.Contains("DRIFTED", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("off", 0)]
    [InlineData("warning", 0)]
    [InlineData("error", ConfigCommands.ConfigurationFailure)]
    public void TheDriftPolicyComesFromSkalaJsonc(string policy, int expected) {
        var directory = TempRoot;
        Directory.CreateDirectory(directory);
        var synced = CanonicalSync.SyncText(
            Path.Combine(directory, ".editorconfig"),
            false,
            string.Empty,
            Tool,
            Payload
        ).Text;
        File.WriteAllText(
            Path.Combine(directory, ".editorconfig"),
            synced.Replace("indent_size = 4", "indent_size = 2", StringComparison.Ordinal)
        );
        File.WriteAllText(
            Path.Combine(directory, ToolConfiguration.FileName),
            $$"""{ "canonical": { "drift": "{{policy}}" } }"""
        );

        Assert.Equal(expected, ConfigCommands.DiffCanonical(directory).ExitCode);
    }

    [Fact]
    public void PinningTheCanonicalVersionInSkalaJsonc_IsAnError() {
        // The pin is the marker, beside the bytes it names. A version in a second file is a version
        // that comes to disagree with itself.
        var config = ToolConfiguration.FromText("/repo/skala.jsonc", """{ "canonical": { "version": "0.1.0" } }""");

        var diagnostic = Assert.Single(
            config.Diagnostics,
            static d => d.Id == ConfigDiagnosticIds.CanonicalVersionInToolConfig
        );
        Assert.Equal(SkalaSeverity.Error, diagnostic.Severity);
    }

    // ── Vixen: an unreviewed config, as a stress case. See RepositoryPaths.VixenEditorConfig ──

    [Fact]
    public void Sync_OnVixen_PreservesEverySectionVerbatim() {
        var vixen = File.ReadAllText(RepositoryPaths.VixenEditorConfig);
        var original = EditorConfigDocument.FromText("vixen/.editorconfig", vixen);
        var expected = original.Sections.Where(static s => s.Name is not null).Select(static s => s.Name!).ToArray();

        var result = CanonicalSync.SyncText(Path.Combine(TempRoot, ".editorconfig"), true, vixen, Tool, Payload);
        var local = EditorConfigDocument.FromText("vixen/.editorconfig", CanonicalLayout.Split(result.Text).LocalText);

        Assert.Equal(56, expected.Length);
        Assert.Equal(
            expected,
            local.Sections.Where(static s => s.Name is not null).Select(static s => s.Name!).ToArray()
        );

        // Not just the section names: every assignment Vixen had, still there, still in its section.
        foreach (var assignment in original.Assignments.Where(static a => a.Section.Name is not null)) {
            Assert.Contains(
                local.Assignments,
                a => a.Section.Name == assignment.Section.Name && a.Key == assignment.Key && a.Value == assignment.Value
            );
        }
    }

    [Fact]
    public void Sync_OnVixen_KeepsTheCommentsThatExplainTheOverrides() {
        // ⚠ Some of Vixen's sections carry reasoning and some do not — it was accumulated by
        // agents rather than authored, and that is exactly why the comments must survive. A sync
        // that dropped them would leave 56 sections nobody *could* review, which is the difference
        // between an override that is auditable and one that is lost.
        var vixen = File.ReadAllText(RepositoryPaths.VixenEditorConfig);
        var result = CanonicalSync.SyncText(Path.Combine(TempRoot, ".editorconfig"), true, vixen, Tool, Payload);
        var local = CanonicalLayout.Split(result.Text).LocalText;

        Assert.Contains("Raven is a compiler for a shading language", local, StringComparison.Ordinal);
        Assert.Contains("A convention that every file violates is not a", local, StringComparison.Ordinal);
    }

    [Theory]
    // A local override in a path-scoped section: editorconfig's own glob does the work.
    [InlineData("Directory.Packages.props", "indent_size", "2")]
    // A local override of a key the canonical sets in `[*]`, from a later `[*.cs]` section.
    [InlineData("Core/Vixen.Core/Thing.cs", "csharp_prefer_braces", "when_multiline:suggestion")]
    // ...and one where the canonical and Vixen disagree outright.
    [InlineData("Core/Vixen.Core/Thing.cs", "trim_trailing_whitespace", "true")]
    // Where Vixen says nothing, the canonical is in force.
    [InlineData("Core/Vixen.Core/Thing.cs", "resharper_csharp_max_line_length", "120")]
    public void Sync_OnVixen_LeavesTheLocalOverridesWinning(string relativePath, string key, string expected) {
        // ⚠ The whole layering argument in one assertion. The canonical block is first, the local
        // block is second, and editorconfig resolves later sections over earlier ones — so a local
        // override survives a canonical bump without Skala having to know it exists.
        //
        // ⚠ "Survives" is deliberately not "is endorsed". Whether these particular overrides should
        // exist is a question for the repository's owner, answered with `SK9013`'s report; Vixen's
        // own answer is that they were never decided and its adoption replaces them.
        var vixen = File.ReadAllText(RepositoryPaths.VixenEditorConfig);
        var root = TempRoot;
        Directory.CreateDirectory(Path.Combine(root, Path.GetDirectoryName(relativePath) ?? "."));
        var path = Path.Combine(root, ".editorconfig");
        File.WriteAllText(path, CanonicalSync.SyncText(path, true, vixen, Tool, Payload).Text);

        var resolution = OptionResolver.Resolve(Path.Combine(root, relativePath));

        Assert.True(OptionRegistry.TryResolve(key, out var id), $"'{key}' is not in the option registry.");
        Assert.Equal(expected, resolution[id].Value);
    }

    [Fact]
    public void Sync_OnVixen_ReportsEveryOverrideRatherThanErasingIt() {
        // The report is the review artefact: "here is what this repository does differently from
        // the canonical, and why" is exactly the conversation a canonical is for.
        var vixen = File.ReadAllText(RepositoryPaths.VixenEditorConfig);
        var path = Path.Combine(TempRoot, ".editorconfig");
        var status = CanonicalSync.Describe(
            path,
            true,
            CanonicalSync.SyncText(path, true, vixen, Tool, Payload).Text,
            Tool,
            Payload
        );

        Assert.Contains(status.Overrides, static local => local.Key == "indent_size" && local.LocalValue == "2");
        Assert.Contains(status.Overrides, static local => local.Key == "trim_trailing_whitespace");
        Assert.Contains(status.Overrides, static local => local.Key == "csharp_prefer_braces");

        // ⚠ The number matters as much as the content: this list is read by a human deciding
        // whether an override is still justified, and Vixen's 56 local sections must not turn into
        // 56 lines of noise. It is 7 today. The bound, not the number, is the property.
        Assert.InRange(status.Overrides.Length, 1, 20);

        // Info, every one of them. An override is the mechanism working.
        var reported = status.Diagnostics.Where(static d => d.Id == ConfigDiagnosticIds.CanonicalLocalOverride)
            .ToArray();
        Assert.Equal(status.Overrides.Length, reported.Length);
        Assert.All(reported, static d => Assert.Equal(SkalaSeverity.Info, d.Severity));
    }

    [Fact]
    public void Sync_OnVixen_IsCleanAfterwards() {
        var vixen = File.ReadAllText(RepositoryPaths.VixenEditorConfig);
        var path = Path.Combine(TempRoot, ".editorconfig");

        var once = CanonicalSync.SyncText(path, true, vixen, Tool, Payload);
        var status = CanonicalSync.Describe(path, true, once.Text, Tool, Payload);
        var twice = CanonicalSync.SyncText(path, true, once.Text, Tool, Payload);

        Assert.True(status.Current);
        Assert.False(twice.Changed);
        Assert.DoesNotContain(status.Diagnostics, static d => d.Severity >= SkalaSeverity.Warning);
    }

    /// <summary>A fresh directory per test. Deleted by the OS; nothing here outlives the run.</summary>
    static string TempRoot {
        get {
            var path = Path.Combine(Path.GetTempPath(), "skala-canonical-tests", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
