using Rikarin.Skala.Options;

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
                    $"'{spelling}' names both '{(seen.TryGetValue(spelling, out var other) ? other : "?")}' and '{info.Key}'");

                Assert.True(OptionRegistry.TryResolve(spelling, out var id));
                Assert.Equal(info.Id, id);
            }
        }
    }

    [Fact]
    public void PlanExample_ResolvesThroughItsLanguageGenericSpelling() {
        // docs/plan/03 § "The option registry" writes this entry out by hand.
        Assert.True(OptionRegistry.TryResolve("resharper_wrap_arguments_style", out var id));
        var info = OptionRegistry.Get(id);
        Assert.Equal("resharper_csharp_wrap_arguments_style", info.Key);
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
            options.GetRaw(OptionId.ResharperCsharpWrapArgumentsStyle),
            (int)options.ReSharper.CSharp.WrapArgumentsStyle);
        Assert.Equal(
            options.GetInt(OptionId.ResharperCsharpMaxLineLength),
            options.ReSharper.CSharp.MaxLineLength);
    }

    [Fact]
    public void TheTemplatesStyle_IsWhatTheRegistrySays() {
        // docs/plan/03 § "The style this config actually describes", spot-checked against the
        // defaults the registry was seeded with.
        var options = FormattingOptions.Defaults;
        Assert.Equal(120, options.ReSharper.CSharp.MaxLineLength);
        Assert.Equal(WrapStyle.ChopIfLong, options.ReSharper.CSharp.WrapArgumentsStyle);
        Assert.Equal(WrapStyle.ChopIfLong, options.ReSharper.CSharp.WrapParametersStyle);
        Assert.Equal(4, options.ReSharper.CSharp.IndentSize);
    }

    [Fact]
    public void NoDefaultIsClaimedVerified_WithoutADocumentationLink() {
        // The distill safety rule: only a resharper-docs default may be dropped, and a
        // resharper-docs default has to point at the page it was read from.
        foreach (var info in OptionRegistry.All.Where(static i => i.DefaultSource == OptionDefaultSource.ReSharperDocs)) {
            Assert.NotNull(info.Docs);
        }
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
        // M0 implements no formatting, so nothing may claim Tier A or B. The six keys the plan
        // names as permanently ignored are Tier C.
        Assert.DoesNotContain(OptionRegistry.All, static i => i.Tier is OptionTier.A or OptionTier.B);

        string[] permanentlyIgnored = [
            "resharper_old_engine",
            "resharper_use_old_engine",
            "resharper_autodetect_indent_settings",
            "resharper_apply_auto_detected_rules",
            "resharper_use_indent_from_vs",
            "resharper_show_autodetect_configure_formatting_tip"
        ];

        foreach (var key in permanentlyIgnored) {
            Assert.True(OptionRegistry.TryResolve(key, out var id), key);
            Assert.Equal(OptionTier.C, OptionRegistry.Get(id).Tier);
        }
    }
}
