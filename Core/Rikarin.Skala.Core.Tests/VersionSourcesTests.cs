using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     docs/plan/18 § "One source of truth": <c>Directory.Build.props</c> carries the version, and
///     everything else in the tree agrees with it or is deliberately independent of it.
/// </summary>
/// <remarks>
///     ⚠ These sit beside <see cref="ProjectGraphTests" /> and <see cref="CanonicalDistributionTests" />
///     because they are the same kind of test: an invariant about the repository, asserted against the
///     real files rather than against a constant. A version scheme enforced by discipline is a version
///     scheme with two versions in it, and this repository already had a second one — <c>rules.json</c>
///     carried <c>"since": "1.2"</c> while <c>Directory.Build.props</c> said <c>1.0.0</c>, describing
///     two releases that never existed.
/// </remarks>
public sealed partial class VersionSourcesTests {
    static string Root => RepositoryPaths.Root;

    /// <summary>The version <c>Directory.Build.props</c> declares, as (major, minor, patch).</summary>
    static (int Major, int Minor, int Patch, string? Suffix) Declared { get; } = ReadDeclared();

    static (int, int, int, string?) ReadDeclared() {
        var document = XDocument.Load(Path.Combine(RepositoryPaths.Root, "Directory.Build.props"));

        string? Value(string name) =>
            document.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.Ordinal))
                ?.Value.Trim();

        var prefix = Value("VersionPrefix")
            ?? throw new InvalidOperationException("Directory.Build.props has no <VersionPrefix>.");
        var parts = prefix.Split('.');
        return (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), Value("VersionSuffix"));
    }

    [Fact]
    public void TheVersion_IsDeclaredExactlyOnce() {
        var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));
        Assert.Single(VersionPrefixElement().Matches(props));

        // ⚠ No project may declare its own. A .csproj with a <Version> silently opts one package out
        // of the release's number, and the five packages ship as a set — doc 11 § "Distribution":
        // one number pins the whole surface, which is what makes a local tool manifest sufficient.
        foreach (var project in Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)) {
            if (ProjectFile.IsScratch(Root, project)) {
                continue;
            }

            var text = File.ReadAllText(project);
            Assert.DoesNotContain("<Version>", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<VersionPrefix>", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<VersionSuffix>", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheDeclaredVersion_IsASemanticVersion() {
        Assert.True(Declared.Major >= 0 && Declared.Minor >= 0 && Declared.Patch >= 0);

        // A suffix is a pre-release identifier, not free text: NuGet orders `alpha.9` before
        // `alpha.10` only because both halves are well-formed.
        if (!string.IsNullOrEmpty(Declared.Suffix)) {
            Assert.Matches(@"^[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*$", Declared.Suffix);
        }
    }

    /// <summary>
    ///     ⚠ <c>since</c> is a version, in two registries, and both were describing releases that had
    ///     not happened.
    /// </summary>
    /// <remarks>
    ///     `rules.json` carried `1.1` and `1.2` against a declared `1.0.0` and zero tags. A `since` in
    ///     the future is not cosmetic: it reaches a consumer through the SARIF (`rules[].properties.
    ///     since`) and through `docs/rules/`, where it is the answer to "can I depend on this rule at
    ///     the version I have pinned".
    /// </remarks>
    [Theory]
    [InlineData("Rules/Rikarin.Skala.Rules.Metadata/rules.json", "rules")]
    [InlineData("Core/Rikarin.Skala.Options/options.json", "options")]
    public void NoRegistryClaimsAVersionThatHasNotBeenReleased(string relativePath, string collection) {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
        );

        var entries = document.RootElement.GetProperty(collection).EnumerateArray().ToList();
        Assert.NotEmpty(entries);

        var checkedAny = false;
        foreach (var entry in entries) {
            if (!entry.TryGetProperty("since", out var since) || since.GetString() is not { Length: > 0 } text) {
                continue;
            }

            checkedAny = true;
            var parts = text.Split('.');
            var version = (
                int.Parse(parts[0]),
                parts.Length > 1 ? int.Parse(parts[1]) : 0,
                parts.Length > 2 ? int.Parse(parts[2]) : 0
            );

            Assert.True(
                version.CompareTo((Declared.Major, Declared.Minor, Declared.Patch)) <= 0,
                $"{relativePath}: '{entry.GetProperty(collection == "rules" ? "id" : "key").GetString()}' "
                + $"declares since = {text}, which is ahead of Directory.Build.props' "
                + $"{Declared.Major}.{Declared.Minor}.{Declared.Patch}. A rule cannot have shipped in a "
                + "version that does not exist."
            );
        }

        // Anti-vacuity: a registry that stopped emitting `since` would pass every assertion above.
        Assert.True(checkedAny, $"{relativePath} has no `since` field on any entry.");
    }

    /// <summary>
    ///     The canonical payload's version is <b>deliberately</b> not the tool's, and nothing may couple
    ///     them.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/02 § "Repository policy" and the remark on <c>Build.CanonicalVersion</c>: a
    ///     canonical bump is a repository-wide reformatting commit and a tool bump is not, so a
    ///     repository must be able to take a bug fix without taking the reformat. This asserts the
    ///     *mechanism* rather than the current values — that the build's canonical version is a literal
    ///     and not derived from <c>VersionPrefix</c> — because two numbers that happen to differ today
    ///     are not two numbers that cannot be joined tomorrow.
    /// </remarks>
    [Fact]
    public void TheCanonicalPayloadVersion_IsNotCoupledToTheToolVersion() {
        using var manifest = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.CanonicalManifest));
        var canonical = manifest.RootElement.GetProperty("version").GetString();
        Assert.False(string.IsNullOrEmpty(canonical));

        var build = File.ReadAllText(Path.Combine(Root, "build", "Build.cs"));
        var declaration = CanonicalVersionParameter().Match(build);
        Assert.True(declaration.Success, "build/Build.cs no longer declares `readonly string CanonicalVersion`.");

        Assert.DoesNotContain("VersionPrefix", declaration.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("$(Version", declaration.Value, StringComparison.Ordinal);

        // And the release tool must not read it either: `skala-release` computing a canonical
        // version would make every tool release a reformat.
        foreach (var source in Directory.EnumerateFiles(
                     Path.Combine(Root, "build", "Rikarin.Skala.Release"),
                     "*.cs",
                     SearchOption.AllDirectories
                 )) {
            foreach (var line in File.ReadLines(source)) {
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal)
                    || code.StartsWith("///", StringComparison.Ordinal)) {
                    continue;
                }

                Assert.DoesNotContain("canonical.json", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    ///     Every branch of <c>ReleasePlan</c>'s baseline selection passes <c>--height</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The no-baseline branch did not, and the version stopped moving.</b>
    ///     <c>ReleasePlan</c> stamps the pre-release counter under <c>DryRun &amp;&amp; Height &gt; 0</c>,
    ///     so with no <c>--height</c> the counter stays 0 and the version is whatever
    ///     <c>Directory.Build.props</c> declares, advanced once. This repository has no <c>v*</c> tag, so
    ///     that branch is the live one: every <c>master</c> build measured <b>2.0.0-alpha.2</b>
    ///     regardless of the commit, the pipeline published it once, and every run after that pushed the
    ///     same bytes and reported "already exists". The symptom looks like a publishing problem and is
    ///     a versioning one.
    ///     <para>
    ///         Asserted over the source text because <c>build/_build.csproj</c> is not in
    ///         <c>Skala.slnx</c> and nothing here can reference it —
    ///         <see cref="TheCanonicalPayloadVersion_IsNotCoupledToTheToolVersion" /> reads it the same
    ///         way. The count is of <c>--height</c> against the three branches that build the argument
    ///         list, so deleting it from any one of them fails.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryBaselineBranch_PassesTheHeight() {
        var build = File.ReadAllText(Path.Combine(Root, "build", "Build.cs"));

        var arguments = build.IndexOf("--baseline-tool", StringComparison.Ordinal);
        Assert.True(arguments > 0, "build/Build.cs no longer builds the release tool's argument list.");

        var target = build[arguments..];
        var end = target.IndexOf("// ⚠ Through the project rather than", StringComparison.Ordinal);
        Assert.True(end > 0, "build/Build.cs no longer ends the baseline selection where this test expects.");

        // ⚠ The quoted literal, not the bare word. Counting `--height` as text counted the sentence
        // in the comment that explains why the argument is there, and read 4 for 3 call sites.
        var branches = target[..end].Split("\"--height\"").Length - 1;
        Assert.Equal(3, branches);
    }

    /// <summary>
    ///     ⚠ The <c>CHANGELOG.md</c> heading a release writes is the one a reader looks the version up
    ///     in, so the top released section may not be ahead of what the tree declares.
    /// </summary>
    [Fact]
    public void TheChangelogsTopReleasedSection_IsNotAheadOfTheDeclaredVersion() {
        var changelog = File.ReadAllText(Path.Combine(Root, "CHANGELOG.md"));
        var heading = ReleasedSection().Match(changelog);
        Assert.True(heading.Success, "CHANGELOG.md has no `## <version> — <date>` section.");

        var parts = heading.Groups["version"].Value.Split('-')[0].Split('.');
        var top = (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));

        Assert.True(
            top.CompareTo((Declared.Major, Declared.Minor, Declared.Patch)) <= 0,
            $"CHANGELOG.md's top released section is {heading.Groups["version"].Value}, ahead of "
            + $"Directory.Build.props' {Declared.Major}.{Declared.Minor}.{Declared.Patch}."
        );
    }

    [GeneratedRegex(@"<VersionPrefix>")]
    private static partial Regex VersionPrefixElement();

    [GeneratedRegex("""readonly string CanonicalVersion\s*=\s*"[^"]*";""")]
    private static partial Regex CanonicalVersionParameter();

    [GeneratedRegex(@"^## (?<version>\d+\.\d+\.\d+[0-9A-Za-z.\-]*) — ", RegexOptions.Multiline)]
    private static partial Regex ReleasedSection();
}
