using System.Text.Json;
using System.Xml.Linq;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// <c>Rikarin.Skala.Sdk</c> is "the one-line adoption" (docs/plan/02 § "Package boundaries"), and on
/// the first repository to try it, it was the one line that could not be taken.
/// </summary>
/// <remarks>
/// ⚠ The Sdk brings <c>Rikarin.Skala.Rules</c>. On Vixen that was 16 <c>SK3002</c> plus 58 further
/// <c>CS0246</c>/<c>CS0234</c> from projects downstream of the ones that failed — not because
/// <c>SK3002</c> ships at <c>error</c>, but because it ships at <c>warning</c> and real repositories
/// set <c>TreatWarningsAsErrors</c>. Doc 09's whole mechanism for a backlog is "accept the present,
/// gate the future", and <c>.skala/baseline.sarif</c> is read by <c>skala check</c> and by nothing
/// else — so that mechanism stopped at the compiler's door.
/// <para>
/// ⚠ These assert the <em>shape</em> of the escape hatches. The behaviour — that a consumer with
/// <c>TreatWarningsAsErrors</c> builds green by default, fails under <c>SkalaRulesAsErrors</c>, and
/// sees no <c>SK</c> diagnostic under <c>SkalaRulesEnabled=false</c> — was verified by packing the
/// real package into a local feed and building a real consumer against it, which is not something a
/// unit test can do in-process.
/// </para>
/// </remarks>
public sealed class SdkAdoptionTests {
    static string TargetsPath =>
        Path.Combine(
            RepositoryPaths.Root,
            "Distribution",
            "Rikarin.Skala.Sdk",
            "build",
            "Rikarin.Skala.Sdk.targets"
        );

    static string Targets => File.ReadAllText(TargetsPath);

    /// <summary>
    /// ⚠ <b>The switch has to be honoured by the package, because NuGet will not honour it.</b>
    /// <c>ExcludeAssets="analyzers"</c> on a reference to the Sdk is silently ineffective:
    /// <c>ExcludeAssets</c> governs the assets of the package it is written on, the Sdk has none,
    /// and the analyzer arrives from a transitive dependency whose nuspec entry says
    /// <c>include="All"</c>. Reproduced against the packed package: <c>project.assets.json</c>
    /// records <c>Rikarin.Skala.Rules</c> with an empty asset list and <c>SK3002</c> still fires
    /// three times. So the opt-out removes the <c>Analyzer</c> item, which always works.
    /// </summary>
    [Fact]
    public void TheSdk_HasAWorkingOptOut() {
        var document = XDocument.Parse(Targets);

        var target = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Target"
                && (string?)element.Attribute("Name") == "SkalaRemoveRules"
            );

        Assert.True(
            target is not null,
            "`SkalaRulesEnabled=false` has to remove the @(Analyzer) item. Nothing a consumer can "
            + "write on the PackageReference takes it back out again."
        );

        Assert.Contains("Analyzer", target!.Descendants().Select(e => e.Name.LocalName));
        Assert.Contains("SkalaRulesEnabled", (string?)target.Attribute("Condition") ?? string.Empty);
    }

    /// <summary>
    /// ⚠ <b>Default false, and the default is the whole answer to the flag day.</b> A repository
    /// that adopts Skala on Monday has a backlog it did not have on Sunday; with
    /// <c>TreatWarningsAsErrors</c> that backlog is a tree that does not build. The diagnostics
    /// still fire at their real severities — nothing is silenced — they simply do not turn a
    /// warning into a build error until the repository says so.
    /// </summary>
    [Fact]
    public void SkalaRulesAsErrors_DefaultsToFalse() {
        Assert.Contains(
            "<SkalaRulesAsErrors Condition=\"'$(SkalaRulesAsErrors)' == ''\">false</SkalaRulesAsErrors>",
            Targets,
            StringComparison.Ordinal
        );

        Assert.Contains("WarningsNotAsErrors", Targets, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ Scoped to Skala's own ids. A package that quietly made a repository's <em>other</em>
    /// warnings non-fatal would be doing the invisible thing this whole design objects to, so the
    /// opt-out lists ids and never touches <c>TreatWarningsAsErrors</c> itself.
    /// </summary>
    [Fact]
    public void TheOptOut_NeverTouchesTreatWarningsAsErrors() {
        var document = XDocument.Parse(Targets);

        Assert.DoesNotContain(
            "TreatWarningsAsErrors",
            document.Descendants()
                .Where(static element => element.Name.LocalName == "TreatWarningsAsErrors")
                .Select(static element => element.Name.LocalName)
        );
    }

    /// <summary>
    /// ⚠ <c>csc</c>'s <c>warnaserror-</c> switch takes ids and has no prefix form, so the list is
    /// hand-written — and a hand-written list of ids drifts. ADR-012 makes ids append-only, so a
    /// rule added without a line here is a rule that silently fails somebody's build on the day
    /// they upgrade.
    /// </summary>
    [Fact]
    public void RuleIds_MatchRulesJson() {
        var document = XDocument.Parse(Targets);
        var declared = document.Descendants()
            .First(static element => element.Name.LocalName == "SkalaRuleIds")
            .Value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();

        using var rules = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryPaths.Root, "Rules", "Rikarin.Skala.Rules.Metadata", "rules.json"))
        );

        // SK0xxx is the formatter and SK9xxx is the tool talking about itself; neither reaches csc
        // as an analyzer diagnostic, so neither belongs in a `warnaserror-` list.
        var expected = rules.RootElement.GetProperty("rules")
            .EnumerateArray()
            .Select(static rule => rule.GetProperty("id").GetString()!)
            .Where(static id => !id.StartsWith("SK0", StringComparison.Ordinal)
                && !id.StartsWith("SK9", StringComparison.Ordinal)
            )
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, declared);
    }

    /// <summary>
    /// The packaged <c>.targets</c> are copied into the package verbatim and parsed for the first
    /// time on a consumer's machine. ⚠ This caught a real one while it was being written: an XML
    /// comment cannot contain <c>--</c>, and a comment explaining <c>skala check --gate=ci</c> made
    /// the file unparseable.
    /// </summary>
    [Fact]
    public void ThePackagedTargets_AreWellFormed() {
        foreach (var file in new[] {
                     TargetsPath,
                     Path.Combine(
                         RepositoryPaths.Root,
                         "Distribution",
                         "Rikarin.Skala.Sdk",
                         "buildTransitive",
                         "Rikarin.Skala.Sdk.targets"
                     )
                 }) {
            var exception = Record.Exception(() => XDocument.Parse(File.ReadAllText(file)));
            Assert.True(exception is null, $"{file} does not parse: {exception?.Message}");
        }
    }
}
