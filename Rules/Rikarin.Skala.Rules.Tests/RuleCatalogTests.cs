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

        var coverage = RuleCoverage.Compute(catalogue, RuleCatalog.All.Select(static rule => rule.Id));

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
        foreach (var rule in RuleCatalog.All.Where(static r => !r.Id.StartsWith("SK9", StringComparison.Ordinal))) {
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
    ///     ⚠ A retired id is still allocated, and the register has to say so.
    /// </summary>
    /// <remarks>
    ///     <c>SK6001</c> and <c>SK7010</c> are one rule under two ids; <c>SK7010</c> shipped.
    ///     <c>SK6001</c> is retired before it was ever built, and until M9 that fact lived only in doc
    ///     08's prose — which <c>allocated-ids.txt</c> cannot read, so nothing stopped the number being
    ///     handed out again. <see cref="RuleIds_AreAppendOnly" /> demands a <c>rules.json</c> entry for
    ///     every allocated id, and a rule that was never built has none, so the register marks the
    ///     retirement inline and the append-only test skips those lines.
    /// </remarks>
    [Fact]
    public void RetiredIds_AreRecordedInTheRegisterAndNotInTheCatalogue() {
        var retired = File.ReadAllLines(AllocatedIdsPath)
            .Where(static line => line.Contains("retired", StringComparison.Ordinal))
            .Where(static line => !line.StartsWith('#'))
            .Select(static line => line.Split(' ')[0])
            .ToList();

        Assert.Contains("SK6001", retired);

        foreach (var id in retired) {
            Assert.False(
                RuleCatalog.All.Any(rule => string.Equals(rule.Id, id, StringComparison.Ordinal)),
                $"{id} is marked retired in allocated-ids.txt and is also in rules.json. "
                + "A retired id names no rule; if this one now ships, it was not retired."
            );
        }
    }

    /// <summary>
    ///     ⚠ The append-only test. An id in <c>allocated-ids.txt</c> must still be in the catalogue with
    ///     the same concept.
    /// </summary>
    [Fact]
    public void RuleIds_AreAppendOnly() {
        var allocated = new List<(string Id, string Concept)>();
        foreach (var line in File.ReadAllLines(AllocatedIdsPath)) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) {
                continue;
            }

            var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
            Assert.True(space > 0, $"'{trimmed}' is not `<id> <concept>`.");

            // ⚠ A retired id has no rules.json entry to compare against, because it was retired
            // before it was ever built. The line stays so the number cannot be handed out twice,
            // which is the only thing this register is for.
            // RetiredIds_AreRecordedInTheRegisterAndNotInTheCatalogue asserts the other half.
            if (trimmed.Contains("retired", StringComparison.Ordinal)) {
                continue;
            }

            allocated.Add((trimmed[..space], trimmed[(space + 1)..].Trim()));
        }

        Assert.NotEmpty(allocated);

        foreach (var (id, concept) in allocated) {
            var rule = RuleCatalog.Find(id);
            Assert.True(
                rule is not null,
                $"{id} is allocated in allocated-ids.txt and is no longer in rules.json. "
                + "ADR-012 makes ids permanent: mark it `retired: true` rather than deleting it, "
                + "because every baseline in every repository names it."
            );

            Assert.True(
                string.Equals(rule!.Concept, concept, StringComparison.Ordinal),
                $"{id} was allocated for '{concept}' and now means '{rule.Concept}'. "
                + "ADR-012: an id's meaning never widens and is never re-purposed. Allocate a new id."
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
}
