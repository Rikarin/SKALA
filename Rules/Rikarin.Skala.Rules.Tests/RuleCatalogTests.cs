using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Rules.Metadata;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The catalogue's invariants, and the one that is a compatibility promise.
/// </summary>
/// <remarks>
///     ⚠ ADR-012: <c>SK1042</c> is allocated once. It may be improved, its severity may change, it may
///     be deprecated and stop firing — it is never reused for a different concept and its meaning never
///     widens. The reason is baselines: a baseline is a set of (rule, file, hash) tuples, and a
///     redefined rule silently un-suppresses or wrongly suppresses findings across every repository
///     that has one.
/// </remarks>
public sealed class RuleCatalogTests {
    static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    static string AllocatedIdsPath { get; } = Path.Combine(
        RepositoryRoot,
        "Rules",
        "Rikarin.Skala.Rules.Metadata",
        "allocated-ids.txt"
    );

    static string CataloguePath { get; } =
        Path.Combine(RepositoryRoot, "docs", "plan", "08-rule-catalogue.md");

    static string ParityMapPath { get; } =
        Path.Combine(RepositoryRoot, "Testing", "parity-analysis", "catalogued.json");

    /// <summary>
    ///     Arrangement findings belong to the formatting band and every declared id is catalogue-backed.
    /// </summary>
    /// <remarks>
    ///     ⚠ These constants once occupied SK2001–SK2017, colliding with the correctness allocation.
    ///     Reading the declaration source here keeps the dependency graph one-way: the metadata tests do
    ///     not need a reference to the formatting implementation in order to guard its public ids.
    /// </remarks>
    [Fact]
    public void ArrangementIds_AreUniqueRegisteredFormattingIds() {
        var path = Path.Combine(
            RepositoryRoot,
            "Formatting",
            "Rikarin.Skala.Formatting.CSharp",
            "Arrangement",
            "ArrangementRule.cs"
        );
        var source = File.ReadAllText(path);
        var ids = System.Text.RegularExpressions.Regex.Matches(
            source,
            "public const string \\w+ = \\\"(?<id>SK\\d{4})\\\";"
        )
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(static match => match.Groups["id"].Value)
            .Where(static id => !id.StartsWith("SK9", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(17, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        foreach (var id in ids) {
            Assert.StartsWith("SK0", id, StringComparison.Ordinal);
            var rule = RuleCatalog.Find(id);
            Assert.NotNull(rule);
            Assert.Equal("Formatting", rule!.Category);
        }
    }

    /// <summary>
    ///     ⚠ The coverage block in doc 08 is generated, and this is what stops it going stale.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It went stale inside one merge: the hand-written table said "21 shipped, 19.8 %", measured
    ///         at <c>8cbd66d</c>, and M8's five <c>SK5xxx</c> landed after it was typed. Nothing compared
    ///         the number to the registry, because nothing could — the number was prose.
    ///     </para>
    ///     <para>
    ///         ⚠ This is a two-directional check, unlike
    ///         <see cref="EveryCatalogueRule_IsNamedInTheRegister" />, and it can be: it does not demand
    ///         that the catalogue and the registry hold the same ids, only that the document's *count* of
    ///         the difference is right. A rule deliberately cut still fails nothing; it moves a row.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheCoverageBlock_MatchesTheRegistry() {
        var catalogue = File.ReadAllText(CataloguePath);

        // Anti-vacuity, the same guard as EveryCatalogueRule_IsNamedInTheRegister: every assertion
        // below is satisfiable by an empty string.
        Assert.True(
            catalogue.Length > 4000 && catalogue.Contains("## SK5000 — Security", StringComparison.Ordinal),
            $"{CataloguePath} was read but does not look like the rule catalogue ({catalogue.Length} bytes)."
        );

        // ⚠ The RuleInfo overload, which drops retired rules itself — the same call the `skala rules
        // docs` generator makes. Filtering here instead would be the drift this test cannot see: the
        // generator would count a withdrawn rule as shipped and the block would never match.
        var coverage = RuleCoverage.Compute(catalogue, RuleCatalog.All);

        // The catalogue names a hundred-odd rules and ships a couple of dozen. If either of those
        // stops being roughly true, the parser has broken rather than the project.
        Assert.True(coverage.Named > 80, $"Only {coverage.Named} rules were found in the catalogue.");
        Assert.True(coverage.Count(RuleCoverage.State.Shipped) > 10, "Almost nothing was matched as shipped.");

        // ⚠ Nothing that ships may be filtered out as a band edge or a range boundary. The filter
        // is worth fifteen ids, so getting it slightly wrong is both easy and invisible: a shipped
        // rule silently dropped from the denominator would make coverage read *better* than it is,
        // which is the one direction an error here must never go.
        //
        // ⚠ SK9xxx is exempt and that is not a loophole. rules.json carries eight tool diagnostics
        // — SK9001 and its neighbours — which are the tool describing itself, not rules from the
        // plan. They have their own register and their own guard in ToolDiagnosticIdTests, and
        // counting them here would credit the catalogue with eight rules it never planned. It is
        // also why rules.json has 37 entries and the coverage says 29.
        foreach (var rule in RuleCatalog.All.Where(static r =>
                     !r.Id.StartsWith("SK9", StringComparison.Ordinal) && !r.Retired
                 )) {
            Assert.True(
                coverage.States.ContainsKey(rule.Id),
                $"{rule.Id} ships and was excluded from the coverage count. "
                + "Check RuleCoverage's band-edge filter — a shipped id is never a range boundary."
            );

            Assert.Equal(RuleCoverage.State.Shipped, coverage.States[rule.Id]);
        }

        Assert.Equal(
            coverage.Named,
            coverage.Count(RuleCoverage.State.Shipped)
            + coverage.Count(RuleCoverage.State.Cut)
            + coverage.Count(RuleCoverage.State.Retired)
            + coverage.Count(RuleCoverage.State.Outstanding)
        );

        var expected = RuleCoverage.Render(coverage);
        var actual = RuleCoverage.Current(catalogue);

        Assert.True(
            actual is not null,
            $"{CataloguePath} has no generated coverage block. The markers are "
            + $"{RuleCoverage.BeginMarker} and {RuleCoverage.EndMarker}."
        );

        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "The coverage block in docs/plan/08-rule-catalogue.md disagrees with rules.json.\n"
            + "Run `skala rules docs` to regenerate it. Expected:\n\n"
            + expected
        );
    }

    /// <summary>
    ///     ⚠ A retired id is still allocated, the register has to say so, and the rule must not fire.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             This test used to assert that a retired id was absent from rules.json, and that was
    ///             only ever true by accident.
    ///         </b> It was written when <c>SK6001</c> was the only retirement
    ///         and <c>SK6001</c> was retired <em>before it was ever built</em>, so it had no
    ///         <c>rules.json</c> row to be present in. Generalising from that one case produced a rule
    ///         that flatly contradicted <see cref="RuleIds_AreAppendOnly" />'s own failure message and
    ///         the <c>notes</c> block at the top of <c>rules.json</c>, both of which say to mark a
    ///         withdrawn rule <c>retired: true</c> — which requires the entry to still be there.
    ///     </para>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             The <c>retired: true</c> field was the built answer all along and had simply never
    ///             been used.
    ///         </b> <c>RuleInfo.Retired</c> is read by five places:
    ///         <c>SkalaRule.Build</c> clears <c>IsEnabledByDefault</c>, <c>AnalyzerHost</c> drops the
    ///         rule from the semantic set, <c>DocsSite</c> renders a retired tag in the index and a
    ///         tombstone banner on the page, <c>RuleFixtures</c> exempts it from the severity check, and
    ///         the release surface reports the transition. All five are dead code unless a withdrawn
    ///         rule keeps its row. Deleting the row instead would 404 the docs page out of its own
    ///         index and leave <c>dotnet_diagnostic.SK1020.severity</c> resolving to nothing in every
    ///         <c>.editorconfig</c> that names it.
    ///     </para>
    ///     <para>
    ///         So retirement has two shapes. Retired after shipping (<c>SK1020</c>, <c>SK1034</c> —
    ///         #281): the row stays, flagged. Retired before it was ever built (<c>SK6001</c>, one rule
    ///         with <c>SK7010</c>, which shipped): there is no row, and this file is the only record.
    ///     </para>
    ///     <para>
    ///         ⚠ The load-bearing assertion is <c>IsEnabledByDefault</c>, not the flag. A retirement
    ///         that set the flag and left the rule firing would satisfy every check that only reads
    ///         metadata about itself.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RetiredIds_AreRecordedInTheRegister_AndNeverShip() {
        var retired = RegisterLines()
            .Where(static entry => entry.Retired)
            .Select(static entry => entry.Id)
            .ToList();

        Assert.Contains("SK6001", retired);
        Assert.Contains("SK1020", retired);
        Assert.Contains("SK1034", retired);

        // Retired before it was ever built: there is no row to flag, and that is the whole reason
        // the register carries the marker inline.
        Assert.Null(RuleCatalog.Find("SK6001"));

        foreach (var id in retired) {
            var rule = RuleCatalog.Find(id);
            if (rule is null) {
                continue;
            }

            Assert.True(
                rule.Retired,
                $"{id} is marked retired in allocated-ids.txt and its rules.json entry is not "
                + "`retired: true`. A withdrawn rule keeps its entry so the docs page and the "
                + "`dotnet_diagnostic` key survive — but the entry has to say it is withdrawn."
            );

            Assert.False(
                SkalaRule.Descriptor(id).IsEnabledByDefault,
                $"{id} is retired and its descriptor is still enabled by default. The flag is not "
                + "the promise; not firing is."
            );
        }

        // The other direction: a flagged row whose register line does not say retired is drift, and
        // the register is the file ADR-012 makes permanent.
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.Retired)) {
            Assert.Contains(rule.Id, retired);
        }
    }

    /// <summary>
    ///     <c>allocated-ids.txt</c> parsed into <c>(id, concept, retired)</c>, comments dropped.
    /// </summary>
    /// <remarks>
    ///     ⚠ The <c>retired</c> marker follows the concept on the same line, so the concept is
    ///     everything before it. Splitting on the marker rather than skipping the line is what lets a
    ///     retired rule's concept still be checked for drift.
    /// </remarks>
    static List<(string Id, string Concept, bool Retired)> RegisterLines() {
        const string marker = " retired";
        var result = new List<(string, string, bool)>();
        foreach (var line in File.ReadAllLines(AllocatedIdsPath)) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) {
                continue;
            }

            var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
            Assert.True(space > 0, $"'{trimmed}' is not `<id> <concept>`.");

            var id = trimmed[..space];
            var rest = trimmed[(space + 1)..].Trim();

            var at = rest.IndexOf(marker, StringComparison.Ordinal);
            var isRetired = at >= 0;
            var concept = isRetired ? rest[..at].Trim() : rest;

            result.Add((id, concept, isRetired));
        }

        Assert.NotEmpty(result);

        return result;
    }

    /// <summary>
    ///     ⚠ The append-only test. An id in <c>allocated-ids.txt</c> must still be in the catalogue with
    ///     the same concept.
    /// </summary>
    [Fact]
    public void RuleIds_AreAppendOnly() {
        foreach (var (id, concept, isRetired) in RegisterLines()) {
            var rule = RuleCatalog.Find(id);

            // ⚠ Only a rule retired BEFORE it was ever built has no entry to compare against.
            // A rule retired after shipping keeps its entry, so its concept is still checked here —
            // the meaning of an id that findings in the wild carry must not drift after withdrawal
            // any more than before it.
            if (rule is null) {
                Assert.True(
                    isRetired,
                    $"{id} is allocated in allocated-ids.txt and is no longer in rules.json. "
                    + "ADR-012 makes ids permanent: mark it `retired: true` in rules.json and add a "
                    + "`retired` marker to its line here, rather than deleting either, because every "
                    + "baseline in every repository names it."
                );

                continue;
            }

            Assert.True(
                string.Equals(rule.Concept, concept, StringComparison.Ordinal),
                $"{id} was allocated for '{concept}' and now means '{rule.Concept}'. "
                + "ADR-012: an id's meaning never widens and is never re-purposed. Allocate a new id."
            );

            Assert.True(
                rule.Retired == isRetired,
                $"{id} is `retired: {rule.Retired.ToString().ToLowerInvariant()}` in rules.json and its "
                + $"allocated-ids.txt line {(isRetired ? "does" : "does not")} carry a `retired` marker. "
                + "The two records of one withdrawal have to agree."
            );
        }
    }

    /// <summary>A new rule must be added to <c>allocated-ids.txt</c> in the same commit.</summary>
    [Fact]
    public void EveryCatalogueRule_IsRecordedAsAllocated() {
        var allocated = File.ReadAllLines(AllocatedIdsPath)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.Split(' ')[0])
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rule in RuleCatalog.All) {
            Assert.True(
                allocated.Contains(rule.Id),
                $"{rule.Id} is in rules.json and not in allocated-ids.txt. "
                + "Add `{rule.Id} {rule.Concept}` there in the same commit."
            );
        }
    }

    /// <summary>
    ///     ⚠ The catalogue in <c>docs/plan/08</c> is the allocation register, and the code drifted away
    ///     from it without anything noticing.
    /// </summary>
    /// <remarks>
    ///     <see cref="RuleIds_AreAppendOnly" /> and <see cref="EveryCatalogueRule_IsRecordedAsAllocated" />
    ///     tie <c>rules.json</c> and <c>allocated-ids.txt</c> to each other, and neither of them looks at
    ///     the document that decides which numbers exist. When this test was written, <c>SK7003</c>,
    ///     <c>SK7004</c> and <c>SK7005</c> were shipping, documented and reported — and named nowhere in
    ///     doc 08, because doc 07's metrics table had grown them instead. ADR-012's whole promise is that
    ///     a number is allocated once, and the only way to keep that promise is to be able to read the
    ///     register and see every number that is taken.
    ///     <para>
    ///         ⚠ <b>One direction only, deliberately.</b> This asserts rules.json ⊆ doc 08. The reverse
    ///         would be wrong: the catalogue names many rules that were considered and <em>cut</em> — with
    ///         reasons, in this document — and demanding doc 08 ⊆ rules.json would turn every recorded
    ///         decision not to build something into a failing build.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryCatalogueRule_IsNamedInTheRegister() {
        var path = Path.Combine(RepositoryRoot, "docs", "plan", "08-rule-catalogue.md");
        Assert.True(File.Exists(path), $"{path} does not exist; the register is what this test reads.");

        var register = File.ReadAllText(path);

        // ⚠ Anti-vacuity. Every assertion below is "this id is present", and all of them pass
        // happily against an empty string — which is exactly how ToolDiagnosticIdTests spent a
        // milestone guarding nothing. If the file being read is not the register, say so here
        // rather than reporting a clean run.
        Assert.True(
            register.Length > 4000 && register.Contains("## SK5000 — Security", StringComparison.Ordinal),
            $"{path} was read but does not look like the rule catalogue ({register.Length} bytes). "
            + "This test proves nothing unless it is reading the register."
        );

        var missing = RuleCatalog.All
            .Where(rule => !register.Contains(rule.Id, StringComparison.Ordinal))
            .Select(static rule => rule.Id + " (" + rule.Concept + ")")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These rules ship and are not named in docs/plan/08-rule-catalogue.md:\n  "
            + string.Join("\n  ", missing)
            + "\n\nAdd them to the catalogue — do not delete the rule. Doc 08 is the allocation "
            + "register ADR-012 depends on, and a number that is taken but not written down is a "
            + "number the next milestone will allocate again."
        );
    }

    [Fact]
    public void EveryRuleId_IsInTheShapeTheRangesDefine() {
        foreach (var rule in RuleCatalog.All) {
            Assert.Matches("^SK[0-9]{4}$", rule.Id);
            Assert.NotEmpty(rule.Title);
            Assert.NotEmpty(rule.Summary);
            Assert.NotEmpty(rule.Rationale);
            Assert.NotEmpty(rule.FalsePositives);
            Assert.NotEmpty(rule.Concept);
        }
    }

    /// <summary>
    ///     ⚠ docs/plan/08's shipping bar, as an invariant: a rule ships when it has a fix. The
    ///     exceptions are the ones that are reports rather than problems — the metrics and the tool's
    ///     own diagnostics — and they say so by being outside the fixable ranges.
    /// </summary>
    [Fact]
    public void EveryModernizationRule_HasAFix() {
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.Category == "Modernization")) {
            Assert.True(rule.HasFix, $"{rule.Id} is a modernization rule with no fix; doc 08's bar is (a) a fix.");
        }
    }

    [Fact]
    public void EveryRuleWithALanguageFloor_DeclaresAParseableVersion() {
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.LanguageVersion is not null)) {
            Assert.Matches("^[0-9]+\\.[0-9]+$", rule.LanguageVersion!);
        }
    }

    /// <summary>
    ///     The ReSharper mapping table, which is docs/plan/16 § Q5's answer in code.
    /// </summary>
    /// <remarks>
    ///     ⚠ Derived from the inspection id rather than stored, so the table cannot drift from the
    ///     rule. The direction that is safe is many-to-one: a ReSharper key may set the severity of
    ///     every Skala rule that maps to it, and <c>dotnet_diagnostic.SK…</c> overrides it. See doc 03
    ///     § "Severities".
    /// </remarks>
    [Fact]
    public void EveryReSharperMapping_ProducesAWellFormedHighlightingKey() {
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.ReSharperId is not null)) {
            var key = rule.ReSharperSeverityKey;
            Assert.NotNull(key);
            Assert.StartsWith("resharper_", key, StringComparison.Ordinal);
            Assert.EndsWith("_highlighting", key, StringComparison.Ordinal);
            Assert.DoesNotContain("__", key, StringComparison.Ordinal);
            Assert.Equal(key.ToLowerInvariant(), key);
        }
    }

    /// <summary>
    ///     ⚠ docs/plan/16 § Q5, as a build-enforced fact: a declared inspection id must be a key the
    ///     real export actually contains.
    /// </summary>
    /// <remarks>
    ///     The derivation from inspection id to key is mechanical, which makes it easy to write down an
    ///     id that snake-cases into a key JetBrains never emits — <c>ConvertToFileScopedNamespace</c>
    ///     and <c>ConvertToThrowIfNull</c> both looked right and neither exists. A mapping to a key
    ///     nothing sets is a mapping that silently never applies, which is the worst kind: it looks like
    ///     a feature and behaves like a comment.
    /// </remarks>
    [Fact]
    public void EveryDeclaredReSharperKey_ExistsInTheExport() {
        var export = Path.Combine(RepositoryRoot, ".editorconfig");
        var keys = File.ReadAllLines(export)
            .Select(static line => line.Split('=')[0].Trim())
            .Where(static key => key.EndsWith("_highlighting", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(keys);

        foreach (var rule in RuleCatalog.All.Where(static rule => rule.ReSharperId is not null)) {
            Assert.True(
                keys.Contains(rule.ReSharperSeverityKey!),
                $"{rule.Id} declares ReSharper inspection '{rule.ReSharperId}', which derives to "
                + $"'{rule.ReSharperSeverityKey}' — and the export sets no such key. "
                + "A mapping nothing can set is a mapping that never applies."
            );
        }
    }

    /// <summary>
    ///     ⚠ The parity measurement's inspection → <c>SK</c> map, pinned in the one direction that is
    ///     assertable: whatever a shipped rule claims from ReSharper, the map must credit to that rule.
    /// </summary>
    /// <remarks>
    ///     <c>Testing/parity-analysis/catalogued.json</c> is hand-written, and its README calls it the
    ///     soft edge of the whole analysis for a reason: an inspection missing from it falls through to
    ///     the <c>Uncovered</c> residue, so an omission does not read as a mistake in the map — it reads
    ///     as a gap in the product. Four rules that ship today were being counted as uncovered for
    ///     exactly that reason, which is what this direction catches.
    ///     <para>
    ///         ⚠ <b>The reverse is deliberately not asserted.</b> The <c>Catalogued</c> bucket means "an
    ///         <c>SK</c> id in doc 08 already names this concept" — allocated is enough and shipped is
    ///         not required, so entries pointing at ids doc 08 names but nothing implements yet are the
    ///         map working correctly. A test demanding map ⊆ <c>rules.json</c> would have to be deleted
    ///         the day one of those is specified. What is checked instead is the weaker fact that holds
    ///         either way: every value is well formed and is a number the register knows about.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheParityMap_CreditsEveryShippedReSharperMappingToItsOwnRule() {
        Assert.True(File.Exists(ParityMapPath), $"{ParityMapPath} does not exist; it is what this test reads.");

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ParityMapPath))) {
            foreach (var entry in document.RootElement.EnumerateObject()) {
                map[entry.Name] = entry.Value.GetString()!;
            }
        }

        // ⚠ Anti-vacuity. Both loops below pass happily against an empty map, and an empty map is
        // the exact shape of the failure this test exists to report.
        Assert.True(map.Count > 100, $"{ParityMapPath} holds {map.Count} entries; that is not the map.");

        // ⚠ Retired rules are excluded. A withdrawn rule keeps its `resharperId` — the entry stays so
        // the descriptor and the docs page survive — but it covers nothing, so the parity map must
        // *not* credit that inspection to it. Crediting it would put the concept in `Catalogued`,
        // which claims Skala answers the inspection, when the honest bucket is `Hosted`.
        var mapped = RuleCatalog.All
            .Where(static rule => rule.ReSharperId is not null && !rule.Retired)
            .ToList();

        Assert.True(mapped.Count >= 10, $"Only {mapped.Count} rules declare a ReSharper inspection id.");

        foreach (var rule in RuleCatalog.All.Where(static rule => rule.Retired && rule.ReSharperId is not null)) {
            Assert.False(
                map.ContainsKey(rule.ReSharperId!),
                $"{rule.ReSharperId} is credited to {rule.Id}, which is retired. A retired rule covers "
                + "nothing; leave the inspection to the Hosted bucket rather than claiming it."
            );
        }

        var uncredited = new List<string>();
        foreach (var rule in mapped) {
            var credited = map.TryGetValue(rule.ReSharperId!, out var id) ? id : null;
            if (!string.Equals(credited, rule.Id, StringComparison.Ordinal)) {
                uncredited.Add($"{rule.ReSharperId} — {rule.Id} ships it, the map says {credited ?? "nothing"}");
            }
        }

        Assert.True(
            uncredited.Count == 0,
            "These shipped rules declare a ReSharper inspection the parity map does not credit to them:\n  "
            + string.Join("\n  ", uncredited)
            + "\n\nDirection asserted: rules.json ⊆ catalogued.json, matched on the SK id. An inspection a "
            + "shipped rule covers and the map omits is counted uncovered by docs/plan/17, which inflates "
            + "the residue and puts work already done back on the queue."
        );

        var register = File.ReadAllText(CataloguePath);
        Assert.True(
            register.Contains("## SK5000 — Security", StringComparison.Ordinal),
            $"{CataloguePath} was read but does not look like the register; the check below proves nothing."
        );

        var shipped = RuleCatalog.All.Select(static rule => rule.Id).ToHashSet(StringComparer.Ordinal);

        // ⚠ This used to read `register.Contains(id)` — a substring match against the whole of
        // doc 08 — and that is not "an id the register knows". Doc 08 names an id in its **cut**
        // tables too, in order to record that it will never be built, and the substring match
        // could not tell the two apart. Five entries went through it that way: `SK2006` (cut, an
        // unassigned `out` parameter is `CS0177`, a compiler error), `SK8003`/`SK8004` (cut,
        // xUnit1001 and xUnit1049 host them) and `SK8001` (cut, no mechanical fix and a large
        // false-positive surface). The map was crediting inspections to rules that had been
        // measured and declined, which reads as coverage in every number downstream.
        //
        // `allocated-ids.txt` is the register ADR-012 actually defines, so it is what this
        // asserts against. `plannedButUnallocated` carries the ids doc 08 specifies and has
        // deliberately not allocated — CLAUDE.md forbids allocating ahead of a specification, so
        // the list is expected to be short, non-empty, and to carry a reason per entry.
        var allocated = File.ReadAllLines(AllocatedIdsPath)
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.Split(' ', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(
            allocated.Count > 100,
            $"{AllocatedIdsPath} lists {allocated.Count} ids; that is not the register."
        );

        var plannedButUnallocated = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["SK1002"] = "doc 08 § SK1000 and § M5: primary constructors are a declaration-shape "
                + "rewrite with no safe fix — deferred, not declined",
            ["SK6004"] = "doc 08 § SK6000: 'the other two remain outstanding' — interface with one "
                + "implementation is specified and not yet allocated"
        };

        var unknown = new List<string>();
        foreach (var (inspection, id) in map) {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^SK[0-9]{4}$")) {
                unknown.Add($"{inspection} -> '{id}' is not a well-formed rule id");
            } else if (!shipped.Contains(id) && !allocated.Contains(id) && !plannedButUnallocated.ContainsKey(id)) {
                unknown.Add($"{inspection} -> {id}, which neither ships nor is allocated in allocated-ids.txt");
            }
        }

        Assert.True(
            unknown.Count == 0,
            "The parity map credits inspections to ids the register does not know:\n  "
            + string.Join("\n  ", unknown)
            + "\n\nDirection asserted: every value is allocated, NOT that every value ships. An id doc 08 "
            + "names and nothing implements yet is a legitimate Catalogued mapping — that is what the "
            + "bucket means — so this is the strongest claim that stays true as the catalogue is built out. "
            + "⚠ An id doc 08 names only in a CUT table is NOT such a mapping: the concept was measured and "
            + "declined, so crediting an inspection to it reports coverage that will never exist."
        );
    }

    /// <summary>
    ///     ⚠ A rule whose ReSharper key the export sets to something surprising must say so.
    /// </summary>
    /// <remarks>
    ///     The measurement behind docs/plan/16 § Q5:
    ///     <c>
    /// resharper_use_throw_if_null_method_highlighting
    ///  = none
    ///     </c>. Any rule whose key is not simply its own default is a rule where reading the key
    ///     changes behaviour, and the note is what makes that a decision rather than a surprise.
    /// </remarks>
    [Fact]
    public void EveryRuleWhoseExportSeverityDiffers_CarriesANote() {
        var export = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Path.Combine(RepositoryRoot, ".editorconfig"))) {
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim().EndsWith("_highlighting", StringComparison.Ordinal)) {
                export[parts[0].Trim()] = parts[1].Trim();
            }
        }

        foreach (var rule in RuleCatalog.All.Where(static rule => rule.ReSharperId is not null)) {
            if (!export.TryGetValue(rule.ReSharperSeverityKey!, out var value)) {
                continue;
            }

            if (!string.Equals(value, rule.DefaultSeverity.ToString().ToLowerInvariant(), StringComparison.Ordinal)) {
                Assert.False(
                    string.IsNullOrEmpty(rule.ReSharperNote),
                    $"{rule.Id} defaults to '{rule.DefaultSeverity.ToString().ToLowerInvariant()}' and the export sets "
                    + $"'{rule.ReSharperSeverityKey}' to '{value}'. Reading the key changes behaviour, so rules.json "
                    + "must carry a `resharperNote` saying so."
                );
            }
        }
    }

    /// <summary>
    ///     ⚠ <c>docs/rules/</c> is generated and never hand-edited (docs/plan/08 § "Documentation").
    /// </summary>
    /// <remarks>
    ///     One source, three surfaces: the docs page, <c>skala explain</c> and the SARIF
    ///     <c>rules[]</c> block are the same <see cref="RuleInfo" /> rendered differently. Without this
    ///     test the first two drift apart silently, and a documentation page that describes the previous
    ///     behaviour is worse than none — a reader has no way to tell which one is stale.
    ///     <para>
    ///         ⚠ It asserts <em>containment</em> rather than byte equality, because
    ///         <c>Rikarin.Skala.Rules.Tests</c> may not reference <c>Analysis</c> — that would put the whole
    ///         analysis stack in the analyzer package's test closure, which is what doc 02's reference test
    ///         exists to prevent — so the renderer itself is not callable from here. Containment still
    ///         catches the failure that matters: a <c>rules.json</c> edit with no <c>skala rules docs</c>
    ///         after it.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DocsPages_AreUpToDate() {
        var directory = Path.Combine(RepositoryRoot, "docs", "rules");
        Assert.True(Directory.Exists(directory), $"{directory} does not exist. Run `skala rules docs`.");
        Assert.True(
            File.Exists(Path.Combine(directory, "README.md")),
            "docs/rules/README.md is missing. Run `skala rules docs`."
        );

        foreach (var rule in RuleCatalog.All) {
            var path = Path.Combine(directory, rule.Id + ".md");
            Assert.True(File.Exists(path), $"{rule.Id} has no page. Run `skala rules docs`.");

            var page = Normalise(File.ReadAllText(path));
            foreach (var (name, text) in new[] {
                         ("title", rule.Title), ("summary", rule.Summary), ("rationale", rule.Rationale),
                         ("falsePositives", rule.FalsePositives)
                     }) {
                Assert.True(
                    page.Contains(Normalise(text), StringComparison.Ordinal),
                    $"docs/rules/{rule.Id}.md does not carry the rule's {name} from rules.json. Run `skala rules docs`."
                );
            }

            if (rule.ReSharperNote is { Length: > 0 } note) {
                Assert.True(
                    page.Contains(Normalise(note), StringComparison.Ordinal),
                    $"docs/rules/{rule.Id}.md does not carry the rule's ReSharper note. Run `skala rules docs`."
                );
            }
        }
    }

    /// <summary>Whitespace-insensitive comparison, because the page is wrapped and the field is not.</summary>
    static string Normalise(string text) {
        var builder = new System.Text.StringBuilder(text.Length);
        var space = false;
        foreach (var c in text) {
            if (char.IsWhiteSpace(c)) {
                space = true;
                continue;
            }

            if (space && builder.Length > 0) {
                builder.Append(' ');
            }

            space = false;
            builder.Append(c);
        }

        return builder.ToString();
    }

    [Fact]
    public void CompilationScopedRules_AreExcludedFromCaching() {
        foreach (var rule in RuleCatalog.All) {
            Assert.Equal(rule.Scope != RuleScope.Compilation, rule.IsCacheable);
        }
    }

    [Fact]
    public void ARuleThatNeedsSemantics_DoesNotClaimToRunWithoutAProject() {
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.RequiresSemantics)) {
            Assert.False(rule.RunsWithoutAProject);
            Assert.NotEqual(RuleScope.Syntax, rule.Scope);
        }
    }

    /// <summary>
    ///     A hosted diagnostic belongs to at most one rule's <c>supersedes</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠ <c>Supersession</c>'s map is a <c>Dictionary&lt;superseded, winner&gt;</c> built by
    ///         <c>map[superseded] = rule.Id</c>, so it <em>structurally cannot hold two owners</em>: a
    ///         second claimant silently overwrites the first, and which one wins is catalogue order.
    ///         Nothing else in the build notices, which is why the invariant the data structure already
    ///         assumes is asserted here rather than left to reading.
    ///     </para>
    ///     <para>
    ///         ⚠ It is scoped to <c>IDE*</c> and <c>CA*</c> deliberately. A Skala id may legitimately be
    ///         superseded by more than one rule — <c>SK4033</c> claimed <c>SK1034</c> until #281 retired
    ///         it — and that is a
    ///         different relationship from hosting an analyzer nobody else may claim.
    ///     </para>
    ///     <para>
    ///         ⚠ This assertion was written for #291 and <b>would not have caught it</b>: <c>SK1015</c>
    ///         claimed <c>IDE0019</c> alone, so there was no duplicate to see. It guards the direction
    ///         the map cannot express, not the direction #291 failed in.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryHostedDiagnostic_IsClaimedByAtMostOneRule() {
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in RuleCatalog.All) {
            foreach (var superseded in rule.Supersedes) {
                if (!superseded.StartsWith("IDE", StringComparison.Ordinal)
                    && !superseded.StartsWith("CA", StringComparison.Ordinal)) {
                    continue;
                }

                if (!owners.TryGetValue(superseded, out var claimants)) {
                    claimants = [];
                    owners[superseded] = claimants;
                }

                claimants.Add(rule.Id);
            }
        }

        var contested = owners
            .Where(static entry => entry.Value.Count > 1)
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => entry.Key + " ← " + string.Join(", ", entry.Value))
            .ToArray();

        Assert.True(
            contested.Length == 0,
            "A hosted diagnostic is claimed by more than one rule's `supersedes`. `Supersession` keeps "
            + "one winner per superseded id, so the later claimant silently replaces the earlier and the "
            + "finding is attributed to a rule that did not produce it:\n  "
            + string.Join("\n  ", contested)
        );

        // ⚠ And the claims are not vacuous: something has to be claimed, or the assertion above passes
        // on an empty map for as long as nobody notices `supersedes` stopped being read.
        Assert.NotEmpty(owners);
    }

    /// <summary>
    ///     Every <c>languageVersion</c> the registry declares is one <c>SkalaRule</c> can map.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Mechanical, cheap, and nothing asserted it.</b> A floor the switch does not name used
    ///     to fall back to <c>Preview</c>, which silences the rule on every real project rather than on
    ///     none — registered, in the SARIF <c>rules[]</c>, in <c>docs/rules/</c>, and reporting nothing
    ///     anywhere. It is not hypothetical: the table had no <c>"6.0"</c> arm while <c>SK1061</c>
    ///     declared <c>6.0</c> as its floor, and it was caught by an agent noticing rather than by
    ///     anything in the build (#296).
    ///     <para>
    ///         ⚠ This is the half of the fix that matters, because it is the half that can be loud. The
    ///         runtime fallback cannot throw — <c>Parse</c> runs inside an analyzer callback, where a
    ///         throw is an <c>AD0001</c> Roslyn swallows — so the registry has to be checked here,
    ///         before anything ships, and the failure has to name the value.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryDeclaredLanguageVersion_IsRecognised() {
        var declared = RuleCatalog.All
            .Where(static rule => !string.IsNullOrEmpty(rule.LanguageVersion))
            .Select(static rule => rule.LanguageVersion!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        // ⚠ The registry has to actually declare some, or this passes by asking nothing — the same
        // vacuous green the `supersedes` map is guarded against above.
        Assert.NotEmpty(declared);

        var unmapped = declared
            .Where(static value => !SkalaRule.TryParseLanguageVersion(value, out _))
            .ToArray();

        Assert.True(
            unmapped.Length == 0,
            "`rules.json` declares a `languageVersion` that `SkalaRule.TryParseLanguageVersion` does "
            + "not name, so every rule carrying it is compared against a floor nobody wrote and fires "
            + "where it should not. Add the arm:\n  "
            + string.Join("\n  ", unmapped)
        );
    }

    /// <summary>
    ///     ⚠ An unrecognised floor over-fires rather than silencing its rule.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The direction is the whole point of #296 and it is worth pinning on its own</b>, because
    ///     the fact above only holds while the registry is the only source of floors. <c>Preview</c> as
    ///     a fallback meant "never fire" — the failure a linter cannot report, since a rule that reports
    ///     nothing looks exactly like a codebase with nothing wrong. <c>Default</c> means "always fire",
    ///     which is noisy and gets attributed to the rule that produced it.
    ///     <para>
    ///         ⚠ The compilation must be pinned to a <em>concrete</em> version for this to measure
    ///         anything. Under <c>LanguageVersion.Preview</c> — the harness default — the old
    ///         <c>Preview</c> fallback also returned <c>true</c>, so the bug and the fix agree and the
    ///         test would pass either way.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnUnrecognisedLanguageVersion_DoesNotSilenceItsRule() {
        var compilation = RuleFixtures.Compile("class C { }", "probe.cs", LanguageVersion.CSharp12);

        Assert.True(
            SkalaRule.MeetsLanguageVersion(compilation, "7.4"),
            "an unrecognised `languageVersion` floor silenced its rule instead of over-firing. That is "
            + "the #296 defect: one typo in `rules.json` and the rule is dead everywhere, with no error "
            + "and nothing in the report to say so."
        );

        // ⚠ And the recognised floors still gate, or the assertion above would pass on a
        // `MeetsLanguageVersion` that had simply stopped comparing anything.
        Assert.False(SkalaRule.MeetsLanguageVersion(compilation, "13.0"));
        Assert.True(SkalaRule.MeetsLanguageVersion(compilation, "12.0"));
    }
}
