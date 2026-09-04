namespace Rikarin.Skala.Options.Tests;

public sealed class OptionRegistryTests {
    [Fact]
    public void Ids_AreDenseAndInOrdinalKeyOrder() {
        // ⚠ The ordering is the contract: ids are baked into the generated FormattingOptions
        // arrays, so an option inserted in the middle must not renumber the ones after it.
        for (var i = 0; i < OptionRegistry.Count; i++) {
            Assert.Equal((OptionId)i, OptionRegistry.All[i].Id);
        }

        var keys = OptionRegistry.All.Select(static info => info.Key).ToArray();
        Assert.Equal(keys.OrderBy(static key => key, StringComparer.Ordinal), keys);
    }

    [Fact]
    public void EverySpelling_ResolvesToExactlyOneOption() {
        // The runtime half of SK9004. The build-time half is in the generator.
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var info in OptionRegistry.All) {
            foreach (var spelling in info.Aliases.Prepend(info.Key)) {
                Assert.True(
                    seen.TryAdd(spelling, info.Key),
                    $"'{spelling}' names both '{(seen.TryGetValue(spelling, out var other) ? other : "?")}' and '{info.Key}'"
                );

                Assert.True(OptionRegistry.TryResolve(spelling, out var id));
                Assert.Equal(info.Id, id);
            }
        }
    }

    [Fact]
    public void PlanExample_ResolvesThroughItsLanguageGenericSpelling() {
        // docs/plan/03 § "The option registry" writes this entry out by hand.
        Assert.True(OptionRegistry.TryResolve("skala_wrap_arguments_style", out var id));
        var info = OptionRegistry.Get(id);
        Assert.Equal("skala_wrap_arguments_style", info.Key);
        Assert.Equal("enum:WrapStyle", "enum:" + info.EnumName);
        Assert.Equal("csharp", info.Language);
    }

    [Fact]
    public void ValueAliases_AreAccepted() {
        // ReSharper writes `true` where the enum member is `always`, and `false` where it is `never`.
        Assert.True(OptionEnums.TryParse("PlacementStyle", "always", out var always));
        Assert.True(OptionEnums.TryParse("PlacementStyle", "true", out var alsoAlways));
        Assert.Equal(always, alsoAlways);

        Assert.True(OptionEnums.TryParse("PlacementStyle", "never", out var never));
        Assert.True(OptionEnums.TryParse("PlacementStyle", "false", out var alsoNever));
        Assert.Equal(never, alsoNever);

        Assert.NotEqual(always, never);
    }

    [Theory]
    [InlineData("wrap_if_long")]
    [InlineData("chop_if_long")]
    [InlineData("chop_always")]
    public void WrapStyle_AcceptsEveryDocumentedValue(string value) {
        Assert.True(OptionEnums.TryParse("WrapStyle", value, out var parsed));
        Assert.Equal(value, OptionEnums.ToText("WrapStyle", parsed));
    }

    [Fact]
    public void Defaults_RoundTripThroughTheStructOfArrays() {
        var options = FormattingOptions.Defaults;
        foreach (var info in OptionRegistry.All) {
            if (info.Default is null) {
                continue;
            }

            var expected = info.SeveritySuffix && info.Default.LastIndexOf(':') is var colon and >= 0
                ? info.Default[..colon].Trim()
                : info.Default;

            if (info.Kind == OptionValueKind.Enum) {
                // The registry may record a value under one of ReSharper's aliases (`false` for
                // `never`); GetText answers with the canonical spelling, which is the same value.
                Assert.True(OptionEnums.TryParse(info.EnumName!, expected, out var wanted));
                Assert.Equal(wanted, options.GetRaw(info.Id));
                continue;
            }

            Assert.Equal(expected, options.GetText(info.Id));
        }
    }

    [Fact]
    public void NamedAccessors_ReadTheSameValueAsTheIndex() {
        var options = FormattingOptions.Defaults;
        Assert.Equal(
            options.GetRaw(OptionId.SkalaWrapArgumentsStyle),
            (int)options.Skala.WrapArgumentsStyle
        );
        Assert.Equal(
            options.GetInt(OptionId.SkalaMaxLineLength),
            options.Skala.MaxLineLength
        );
    }

    [Fact]
    public void TheTemplatesStyle_IsWhatTheRegistrySays() {
        // docs/plan/03 § "The style this config actually describes", spot-checked against the
        // defaults the registry was seeded with.
        var options = FormattingOptions.Defaults;
        Assert.Equal(120, options.Skala.MaxLineLength);
        Assert.Equal(WrapStyle.ChopIfLong, options.Skala.WrapArgumentsStyle);
        Assert.Equal(WrapStyle.ChopIfLong, options.Skala.WrapParametersStyle);
        Assert.Equal(4, options.Skala.IndentSize);
    }

    /// <summary>
    ///     Every option reached the model with the export spelling the oracle needs.
    /// </summary>
    /// <remarks>
    ///     ⚠ This replaces <c>NoDefaultIsClaimedVerified_WithoutADocumentationLink</c>, which asserted
    ///     that every <c>ReSharperDocs</c> default carried a <c>Docs</c> link and was <b>vacuous</b>:
    ///     no entry in this registry has ever had that default source — options.json's own note says
    ///     the published tables state no defaults — so the loop ran zero times and the assertion never
    ///     executed. It is exactly the "a zero from a disabled check and a zero from clean code are the
    ///     same zero" shape.
    ///     <para>
    ///         What is asserted instead is the invariant the <c>docs</c> removal made load-bearing: the
    ///         export spellings now live in <c>export-bridge.json</c>, they are joined at generation
    ///         time, and an option that lost its entry would be handed its own <c>skala_</c> key by
    ///         <c>ExportSpellings.ForOracle</c> — which the oracle ignores without a word.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryOption_CarriesTheExportsSpellingForIt() {
        Assert.NotEmpty(OptionRegistry.All);
        foreach (var info in OptionRegistry.All) {
            Assert.True(
                info.Export.Count > 0,
                $"'{info.Key}' has no entry in export-bridge.json. The oracle would be handed "
                + $"'{info.Key}', a key it does not recognise and silently ignores, and the sweep would "
                + "report the option INERT."
            );
        }
    }

    /// <summary>
    ///     ⚠ No export spelling is reachable through the registry, and that is the whole rename.
    /// </summary>
    /// <remarks>
    ///     The moment one resolves, pointing Skala at a Rider export configures it again and nothing
    ///     else notices: <c>config check</c> stops reporting SK9001 for keys Skala is not supposed to
    ///     read, and the canonical payload's translation step becomes a copy.
    /// </remarks>
    [Fact]
    public void NoExportSpelling_ResolvesThroughTheRegistry() {
        var checkedAny = false;
        foreach (var info in OptionRegistry.All) {
            foreach (var spelling in info.Export) {
                if (spelling.StartsWith("resharper_", StringComparison.Ordinal)) {
                    checkedAny = true;
                    Assert.False(
                        OptionRegistry.TryResolve(spelling, out _),
                        $"'{spelling}' resolves through OptionRegistry; it is provenance, not an alias."
                    );
                }
            }
        }

        // Anti-vacuity: a bridge that stopped carrying prefixed spellings would pass silently.
        Assert.True(checkedAny, "no export spelling in the registry is in the export's own namespace.");
    }

    [Fact]
    public void GeneralizedProperties_ExpandToOptionsThatExist() {
        foreach (var info in OptionRegistry.All.Where(static i => i.Expands.Count > 0)) {
            foreach (var target in info.Expands) {
                Assert.NotEqual(info.Id, target);
                Assert.Equal(target, OptionRegistry.Get(target).Id);
            }
        }
    }

    [Fact]
    public void Tiers_AreHonest() {
        // ⚠ Tier A means "implemented, and pinned by at least one oracle fixture". It may never
        // rest on a default being known: defaultSource is `template` or `unknown` for every entry
        // in this registry and there is no verified default table (docs/plan/03 § "distill",
        // corrected in d081293). So the only evidence a Tier A claim can carry is an `oracle` glob,
        // and this test is the half of that rule the registry can check on its own — the other half,
        // that the glob names a corpus file which demonstrably changes behaviour, is
        // OptionCoverageTests in the conformance suite.
        //
        // M0 forbade Tier A outright, because M0 implemented no formatting. M1 implements 138 keys.
        foreach (var info in OptionRegistry.All.Where(static i => i.Tier is OptionTier.A or OptionTier.B)) {
            Assert.True(
                info.Oracle is { Length: > 0 },
                $"{info.Key} claims Tier {info.Tier} with no `oracle` fixture glob. A tier claim is evidence, not an intention."
            );

            Assert.NotEqual(OptionDefaultSource.ReSharperDocs, info.DefaultSource);
        }

        // ⚠ A Tier D entry carrying an `oracle` glob used to be forbidden outright, on the reading
        // that a fixture nobody claims is a promotion someone forgot. The key-flip sweep made that
        // reading wrong: it demoted 70 options that have fixtures, that the fixtures do exercise,
        // and whose output disagrees with ReSharper's. "Fixtured and measured not to conform" is
        // more evidence than Tier D usually carries, not less.
        //
        // ⚠ The glob must stay on those entries. `SweepPlan` sweeps exactly the options that have
        // one, so stripping it would drop all 70 out of the next sweep — the demotion could never be
        // re-measured, and a key that was fixed would never be promoted back. That is precisely the
        // unfalsifiable-verdict shape this harness exists to avoid.
        //
        // What replaces the blanket rule is the pairing: a Tier D option may carry a glob only if
        // the committed sweep records it as non-conformant. That needs the sweep's sidecar, which
        // this project cannot reach, so it is asserted in the conformance suite instead —
        // OptionCoverageTests.TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt.

        // ⚠ Five, not six. `resharper_show_autodetect_configure_formatting_tip` was the sixth and is
        // deleted: it governs whether an IDE shows a notification, nothing in Skala read it, and it
        // is the one Tier C entry that no production literal or oracle fixture referred to.
        string[] permanentlyIgnored = [
            "skala_old_engine", "skala_use_old_engine", "skala_autodetect_indent_settings",
            "skala_apply_auto_detected_rules", "skala_use_indent_from_vs"
        ];

        foreach (var key in permanentlyIgnored) {
            Assert.True(OptionRegistry.TryResolve(key, out var id), key);
            Assert.Equal(OptionTier.C, OptionRegistry.Get(id).Tier);
        }

        // The set is closed: a sixth Tier C entry is a decision, not a drift.
        Assert.Equal(
            permanentlyIgnored.Length,
            OptionRegistry.All.Count(static info => info.Tier == OptionTier.C)
        );
    }

    /// <summary>
    ///     An option a standalone analyzer reads by literal has exactly one spelling.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Rikarin.Skala.Rules</c> deliberately does not reference this assembly — an analyzer
    ///     ships on its own — so <c>ConfigureAwaitAnalyzer</c> cannot ask the registry and hard-codes
    ///     the key. It used to hard-code a *precedence ladder* over two spellings as well, which was a
    ///     second implementation of <see cref="OptionResolver" />'s rule that nothing kept in step. The
    ///     ladder is gone; this is what makes its absence safe, because the analyzer cannot notice an
    ///     alias being added and would simply stop honouring configurations written that way.
    /// </remarks>
    [Fact]
    public void TheAnalyzerReadOptions_HaveExactlyOneSpelling() {
        foreach (var key in new[] { "skala_configure_await_analysis_mode" }) {
            Assert.True(OptionRegistry.TryResolve(key, out var id), key);

            var info = OptionRegistry.Get(id);
            Assert.Equal(key, info.Key);
            Assert.True(
                info.Aliases.Count == 0,
                $"{key} has picked up the alias(es) {string.Join(", ", info.Aliases)}. ConfigureAwaitAnalyzer "
                + "reads this key by literal and cannot see them, so a configuration written that way "
                + "would be silently ignored. Teach the analyzer the spelling, then relax this."
            );
        }
    }

    /// <summary>
    ///     Every property the EditorConfig specification defines resolves under its own name.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>These are not Skala's to rename, and one of them was lost renaming the rest.</b>
    ///     <c>insert_final_newline</c> existed only as an alias of ReSharper's
    ///     <c>resharper_csharp_insert_final_newline</c>, so dropping ReSharper's alias spellings took
    ///     it with them — and nothing failed, because no test named the bare spelling. A consuming
    ///     repository setting the single most common line in any <c>.editorconfig</c> would have got
    ///     <c>SK9001</c> and silently lost the setting.
    ///     <para>
    ///         The same reasoning covers Microsoft's own keys, which is why
    ///         <c>dotnet_sort_system_directives_first</c> and <c>csharp_space_after_cast</c> survive as
    ///         aliases: the <c>resharper_</c> namespace is Skala's to retire; the EditorConfig and
    ///         <c>dotnet_</c>/<c>csharp_</c> namespaces are not.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EveryEditorConfigCoreProperty_ResolvesUnderItsOwnName() {
        string[] core = [
            "indent_style", "indent_size", "tab_width", "end_of_line", "charset",
            "trim_trailing_whitespace", "insert_final_newline", "max_line_length"
        ];

        foreach (var key in core) {
            Assert.True(
                OptionRegistry.TryResolve(key, out _),
                key
                + " is defined by the EditorConfig specification and must resolve under that "
                + "spelling. Skala's namespace is `skala_`; this one is not Skala's to rename."
            );
        }
    }

    /// <summary>
    ///     ⚠ Inert is a claim about what cannot be observed, so it needs a reason and it needs a tier.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         An inert option is Tier D — nothing implements it — but it is <b>not</b> a coverage gap:
    ///         no input distinguishes its values, because another rule wins by the documented ordering or
    ///         because the writer cannot produce the shape it governs. <c>skala config check</c> reports
    ///         the two separately, and that report is only worth reading if "inert" is evidence rather
    ///         than a way to make a number look better.
    ///     </para>
    ///     <para>
    ///         ⚠ An inert option that claimed Tier A would be the worse failure: it would mean a fixture
    ///         pinned behaviour that no input can produce. The registry forbids the pair.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Inert_OptionsCarryAReasonAndAreNotClaimedAsImplemented() {
        var inert = OptionRegistry.All.Where(static i => i.Inert is not null).ToList();

        // ⚠ Anti-vacuity, and the floor used to be 10 against a population of 97 — nine tenths of
        // the inert set could have vanished without this firing, which is not a canary, it is a
        // decoration. It is a ratchet now: the count at the commit that set it. Moving it down is a
        // deliberate edit that says which options stopped being inert and why.
        const int Measured = 52;
        Assert.True(
            inert.Count >= Measured,
            $"Only {inert.Count} inert options, against {Measured} measured. An option stops being inert when something reads it, which is a promotion and needs a fixture — not a quiet drop."
        );

        foreach (var info in inert) {
            Assert.Equal(OptionTier.D, info.Tier);
            Assert.True(
                info.Inert is { Length: > 20 },
                $"{info.Key} is marked inert with no usable reason. \"Inert\" without a reason is indistinguishable from \"unimplemented\", which is the distinction the mark exists to make."
            );
        }
    }

    /// <summary>
    ///     ⚠ <c>UnsweptBecause</c> is the other half of "why is there no fixture", and it must not be
    ///     allowed to become a second spelling of <c>Inert</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The two answer different questions and the difference is the whole point. <c>Inert</c> says
    ///         <b>no input distinguishes the values</b> — a claim about the world, so setting the option
    ///         costs nothing and it is honoured vacuously. <c>UnsweptBecause</c> says
    ///         <b>the sweep cannot ask</b>, and that is usually a fact about Skala rather than about
    ///         the option: the oracle separates the values and Skala is
    ///         flat at every one of them, so a glob would file an <c>INERT</c> row the registry never
    ///         declared, against a fixture Skala cannot reproduce. Merging them would report a real,
    ///         measured gap as a vacuous one, which is the exact defect the inert mark exists to prevent
    ///         in the other direction.
    ///     </para>
    ///     <para>
    ///         ⚠ It is also where a Tier C refusal's measurement goes. <c>Inert</c> is Tier D by
    ///         construction — the assertion above — so a Tier C key that was <em>also</em> measured
    ///         unobservable had nowhere to record it, and
    ///         <c>skala_use_old_engine</c> sat with an empty entry for that reason. Recording it here
    ///         keeps the refusal and the measurement distinct: Tier C still means Skala declines, and
    ///         <c>skala_old_engine</c> — the sibling the oracle demonstrably honours — is why
    ///         that is not the same claim.
    ///     </para>
    /// </remarks>
    [Fact]
    public void UnsweptBecause_IsNotASecondSpellingOfInert() {
        foreach (var info in OptionRegistry.All.Where(static i => i.UnsweptBecause is not null)) {
            Assert.True(
                info.UnsweptBecause is { Length: > 20 },
                $"{info.Key} carries `unsweptBecause` with no usable reason."
            );

            Assert.True(
                info.Inert is null,
                $"{info.Key} carries both `inert` and `unsweptBecause`. They are two answers to one question: `inert` claims no input can distinguish the values, `unsweptBecause` says the sweep cannot ask. Pick the one the probe actually established."
            );

            Assert.True(
                info.Oracle is null,
                $"{info.Key} names an oracle fixture and also says why it has none."
            );

            Assert.NotEqual(OptionTier.A, info.Tier);
        }
    }
}
