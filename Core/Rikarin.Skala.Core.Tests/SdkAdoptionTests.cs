using System.Text.Json;
using System.Xml.Linq;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     <c>Rikarin.Skala.Sdk</c> is "the one-line adoption" (docs/plan/02 § "Package boundaries"), and on
///     the first repository to try it, it was the one line that could not be taken.
/// </summary>
/// <remarks>
///     ⚠ The Sdk brings <c>Rikarin.Skala.Rules</c>. On Vixen that was 16 <c>SK3002</c> plus 58 further
///     <c>CS0246</c>/<c>CS0234</c> from projects downstream of the ones that failed — not because
///     <c>SK3002</c> ships at <c>error</c>, but because it ships at <c>warning</c> and real repositories
///     set <c>TreatWarningsAsErrors</c>. Doc 09's whole mechanism for a backlog is "accept the present,
///     gate the future", and <c>.skala/baseline.sarif</c> is read by <c>skala check</c> and by nothing
///     else — so that mechanism stopped at the compiler's door.
///     <para>
///         ⚠ These assert the <em>shape</em> of the escape hatches. The behaviour — that a consumer with
///         <c>TreatWarningsAsErrors</c> builds green by default, fails under <c>SkalaRulesAsErrors</c>, and
///         sees no <c>SK</c> diagnostic under <c>SkalaRulesEnabled=false</c> — was verified by packing the
///         real package into a local feed and building a real consumer against it, which is not something a
///         unit test can do in-process.
///     </para>
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

    /// <summary>
    ///     ⚠ Generated, not typed. <c>SkalaGenerateRuleIds</c> in
    ///     <c>Distribution/Rikarin.Skala.Sdk/Rikarin.Skala.Sdk.csproj</c> rewrites this file from
    ///     <c>rules.json</c> on every build of that project, and the <c>.targets</c> imports it.
    /// </summary>
    static string RuleIdsPath =>
        Path.Combine(
            RepositoryPaths.Root,
            "Distribution",
            "Rikarin.Skala.Sdk",
            "build",
            "Rikarin.Skala.Sdk.RuleIds.props"
        );

    static string TransitiveTargetsPath =>
        Path.Combine(
            RepositoryPaths.Root,
            "Distribution",
            "Rikarin.Skala.Sdk",
            "buildTransitive",
            "Rikarin.Skala.Sdk.targets"
        );

    /// <summary>Every MSBuild file this package copies into the .nupkg verbatim.</summary>
    static string[] PackagedMSBuildFiles => [TargetsPath, RuleIdsPath, TransitiveTargetsPath];

    static string Targets => File.ReadAllText(TargetsPath);

    /// <summary>
    ///     ⚠ <b>The switch has to be honoured by the package, because NuGet will not honour it.</b>
    ///     <c>ExcludeAssets="analyzers"</c> on a reference to the Sdk is silently ineffective:
    ///     <c>ExcludeAssets</c> governs the assets of the package it is written on, the Sdk has none,
    ///     and the analyzer arrives from a transitive dependency whose nuspec entry says
    ///     <c>include="All"</c>. Reproduced against the packed package: <c>project.assets.json</c>
    ///     records <c>Rikarin.Skala.Rules</c> with an empty asset list and <c>SK3002</c> still fires
    ///     three times. So the opt-out removes the <c>Analyzer</c> item, which always works.
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
    ///     ⚠ <b>Default false, and the default is the whole answer to the flag day.</b> A repository
    ///     that adopts Skala on Monday has a backlog it did not have on Sunday; with
    ///     <c>TreatWarningsAsErrors</c> that backlog is a tree that does not build. The diagnostics
    ///     still fire at their real severities — nothing is silenced — they simply do not turn a
    ///     warning into a build error until the repository says so.
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
    ///     ⚠ Scoped to Skala's own ids. A package that quietly made a repository's <em>other</em>
    ///     warnings non-fatal would be doing the invisible thing this whole design objects to, so the
    ///     opt-out lists ids and never touches <c>TreatWarningsAsErrors</c> itself.
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
    ///     ⚠ <c>csc</c>'s <c>warnaserror-</c> switch takes ids and has no prefix form, which is why
    ///     the value is a list of ids at all — and for the whole of the catalogue's growth that list
    ///     was <em>typed</em>, which is a different thing and was never justified by the first. It
    ///     sat at 36 ids while <c>rules.json</c> shipped 101, and a rule missing from it turns a
    ///     warning into a build error in every consumer with <c>TreatWarningsAsErrors</c>.
    ///     <para>
    ///         It is now generated by <c>SkalaGenerateRuleIds</c> in the package's <c>.csproj</c>, so
    ///         this test no longer catches a person forgetting to type an id. ⚠ What it still catches
    ///         is the generated file not being rebuilt or not being committed — <c>dotnet test</c>
    ///         does not build <c>Rikarin.Skala.Sdk</c>, so a rules.json merge lands here red until
    ///         somebody runs the build.
    ///     </para>
    /// </summary>
    [Fact]
    public void RuleIds_MatchRulesJson() {
        var document = XDocument.Parse(File.ReadAllText(RuleIdsPath));
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

        // ⚠ Not `Assert.Equal(expected, declared)`. That prints "collections differ at index 1" and
        // the first pair of ids, which says nothing about the other sixty-four — the drift this
        // catches is a batch of new rules, never one.
        var missing = expected.Except(declared, StringComparer.Ordinal).ToArray();
        var extra = declared.Except(expected, StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0 && extra.Length == 0,
            $"<SkalaRuleIds> in {RuleIdsPath} is out of step with rules.json."
            + (missing.Length > 0
                    ? Environment.NewLine
                    + $"  in rules.json and not declared ({missing.Length}): "
                    + string.Join(";", missing)
                    : string.Empty)
            + (extra.Length > 0
                    ? Environment.NewLine
                    + $"  declared and not in rules.json ({extra.Length}): "
                    + string.Join(";", extra)
                    : string.Empty)
            + Environment.NewLine
            + "That file is generated: run `dotnet build Distribution/Rikarin.Skala.Sdk` and commit "
            + "the result. Do not edit it — the next build overwrites it."
        );
    }

    /// <summary>
    ///     ⚠ <b>A merge once produced two <c>&lt;SkalaRuleIds&gt;</c> elements.</b> The file was still
    ///     well-formed XML, MSBuild silently took one of them, and the wrong value parsed — so
    ///     <see cref="RuleIds_MatchRulesJson" />, which reads the first match, stayed green. It was
    ///     caught by hand.
    ///     <para>
    ///         This is the assertion that was missing. It counts declarations across every MSBuild
    ///         file the package ships, so the id list existing in two places at once is a failing
    ///         test wherever the second one is: a duplicated element, a stray copy left in the
    ///         <c>.targets</c> after the generator took over, or the <c>buildTransitive</c> shim
    ///         growing one of its own.
    ///     </para>
    /// </summary>
    [Fact]
    public void TheRuleIdList_IsDeclaredExactlyOnce() {
        var declarations = PackagedMSBuildFiles
            .SelectMany(file => XDocument.Parse(File.ReadAllText(file))
                .Descendants()
                .Where(static element => element.Name.LocalName == "SkalaRuleIds")
                .Select(_ => file)
            )
            .ToArray();

        Assert.True(
            declarations.Length == 1,
            $"<SkalaRuleIds> is declared {declarations.Length} times across the packaged MSBuild "
            + "files; exactly one declaration, in the generated Rikarin.Skala.Sdk.RuleIds.props, is "
            + "the only shape MSBuild resolves unambiguously."
            + Environment.NewLine
            + "  declared in: "
            + (declarations.Length == 0 ? "(nowhere)" : string.Join(", ", declarations))
        );

        Assert.Equal(RuleIdsPath, declarations[0]);
    }

    /// <summary>
    ///     ⚠ The <c>.targets</c> is the file ten concurrent rule branches all touch, and the import
    ///     is what keeps the id list out of it. A generated value that nothing imports is a value
    ///     nobody uses, and the escape hatch would be off with no diagnostic anywhere.
    /// </summary>
    [Fact]
    public void TheTargets_ImportTheGeneratedRuleIds() {
        var document = XDocument.Parse(Targets);

        var imports = document.Descendants()
            .Where(static element => element.Name.LocalName == "Import")
            .Select(static element => (string?)element.Attribute("Project") ?? string.Empty)
            .ToArray();

        Assert.Contains(
            imports,
            import => import.Contains("Rikarin.Skala.Sdk.RuleIds.props", StringComparison.Ordinal)
        );

        // ⚠ Unconditional. `Condition="Exists(...)"` would turn a missing generated file into an
        // empty $(SkalaRuleIds) — a WarningsNotAsErrors that silences nothing — instead of MSB4019.
        Assert.DoesNotContain(
            document.Descendants()
                .Where(static element => element.Name.LocalName == "Import")
                .Where(static element =>
                    ((string?)element.Attribute("Project") ?? string.Empty)
                    .Contains("RuleIds.props", StringComparison.Ordinal)
                ),
            element => element.Attribute("Condition") is not null
        );
    }

    /// <summary>
    ///     The packaged <c>.targets</c> are copied into the package verbatim and parsed for the first
    ///     time on a consumer's machine. ⚠ This caught a real one while it was being written: an XML
    ///     comment cannot contain <c>--</c>, and a comment explaining <c>skala check --gate=ci</c> made
    ///     the file unparseable.
    /// </summary>
    [Fact]
    public void ThePackagedTargets_AreWellFormed() {
        foreach (var file in PackagedMSBuildFiles) {
            var exception = Record.Exception(() => XDocument.Parse(File.ReadAllText(file)));
            Assert.True(exception is null, $"{file} does not parse: {exception?.Message}");
        }
    }
}
