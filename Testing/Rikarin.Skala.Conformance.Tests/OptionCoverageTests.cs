using System.Globalization;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// Level 1 of docs/plan/12: the option units, and the floor under Tier A.
/// </summary>
/// <remarks>
/// ⚠ "Every entry in <c>options.json</c> requires at least one corpus file in <c>constructs/</c>
/// that changes behaviour when the option changes." An option with no observable effect is either
/// unimplemented or wrongly wired — both are bugs, and both are build failures here.
/// <para>
/// The test is scoped to the options the formatter actually reads. It is inert for the rest, which
/// is the honest state of a milestone that implements 138 of ~380 keys, and it becomes a wider net
/// with no change to this file as each later phase promotes its own.
/// </para>
/// </remarks>
public sealed class OptionCoverageTests {
    public static TheoryData<string> ImplementedKeys {
        get {
            var data = new TheoryData<string>();
            foreach (var id in PhaseOneOptions.Implemented) {
                data.Add(OptionRegistry.Get(id).Key);
            }

            return data;
        }
    }

    [Fact]
    public void TierA_IsExactlyWhatTheFormatterReads() {
        // ⚠ Tier A means "Skala reproduces Rider's behaviour, pinned by at least one oracle
        // fixture" (docs/plan/03 § "Four tiers"). It may not rest on a default being known:
        // defaultSource is `template` or `unknown` for every entry in the registry, so the only
        // available evidence is a fixture (docs/plan/03 § "distill", corrected in d081293).
        var implemented = PhaseOneOptions.Implemented.ToHashSet();
        var claimed = OptionRegistry.All.Where(static info => info.Tier == OptionTier.A).Select(static info => info.Id).ToHashSet();

        var overclaimed = claimed.Except(implemented).Select(static id => OptionRegistry.Get(id).Key).Order(
            StringComparer.Ordinal
        ).ToArray();
        Assert.True(overclaimed.Length == 0, "Tier A without an implementation: " + string.Join(", ", overclaimed));

        var underclaimed = implemented.Except(claimed).Select(static id => OptionRegistry.Get(id).Key).Order(
            StringComparer.Ordinal
        ).ToArray();
        Assert.True(underclaimed.Length == 0, "Implemented but not Tier A: " + string.Join(", ", underclaimed));
    }

    [Fact]
    public void NoOptionClaimsTierB_WithoutADocumentedDivergence() {
        foreach (var info in OptionRegistry.All.Where(static i => i.Tier == OptionTier.B)) {
            Assert.True(
                Divergences.Register.Any(entry => entry.Options.Contains(info.Key, StringComparer.Ordinal)),
                $"{info.Key} is Tier B, which means 'implemented with a documented divergence'. docs/divergences.md does not mention it."
            );
        }
    }

    [Theory]
    [MemberData(nameof(ImplementedKeys))]
    public void EveryImplementedOption_IsPinnedByACorpusFileWithAnOracleFixture(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);

        Assert.True(info.Oracle is not null, $"{key} is implemented but its registry entry has no `oracle` glob.");
        var files = Resolve(info.Oracle!);
        Assert.True(files.Count > 0, $"{key}: `oracle` is '{info.Oracle}' and no corpus file matches it.");
        Assert.True(
            files.Any(static file => file.HasFixture),
            $"{key}: no committed .expected.cs beside its corpus file. Tier A rests on fixture evidence and nothing else; run ./build.sh Oracle."
        );
    }

    [Theory]
    [MemberData(nameof(ImplementedKeys))]
    public void EveryImplementedOption_ChangesTheOutputOfItsCorpusFile(string key) {
        Assert.True(OptionRegistry.TryResolve(key, out var id));
        var info = OptionRegistry.Get(id);
        var files = Resolve(info.Oracle!);

        var values = LegalValues(info).ToArray();
        Assert.True(values.Length >= 2, $"{key}: fewer than two values to compare.");

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in values) {
                var builder = new FormattingOptionsBuilder();
                if (!builder.TrySet(id, value, out var error)) {
                    Assert.Fail($"{key} = {value}: {error}");
                }

                outputs[value] = CSharpFormatter.Format(file.Path, text, new PhaseOneOptions(builder.Build())).Formatted;
            }

            if (outputs.Values.Distinct(StringComparer.Ordinal).Count() > 1) {
                return;
            }
        }

        Assert.Fail(
            $"{key}: setting it to any of [{string.Join(", ", values)}] produces byte-identical output on "
            + $"[{string.Join(", ", files.Select(static f => f.ToString()))}]. An option with no observable effect is "
            + "either unimplemented or wrongly wired; both are bugs."
        );
    }

    /// <summary>
    /// For a bool, both values. For an enum, its whole domain. For an int, the configured value and
    /// one that is definitely different — an int's domain is unbounded and the point of the test is
    /// observability, not exhaustiveness.
    /// </summary>
    static IEnumerable<string> LegalValues(OptionInfo info) {
        switch (info.Kind) {
            case OptionValueKind.Bool:
                yield return "true";
                yield return "false";
                break;

            case OptionValueKind.Enum:
                foreach (var value in OptionEnums.ValuesOf(info.EnumName!)) {
                    yield return value;
                }

                break;

            case OptionValueKind.Int:
                var current = int.TryParse(
                    info.Default,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var number
                ) ? number : 0;
                yield return current.ToString(CultureInfo.InvariantCulture);
                yield return (current == 0 ? 3 : current == 1 ? 2 : 0).ToString(CultureInfo.InvariantCulture);
                break;

            default:
                yield return info.Default ?? string.Empty;
                yield return info.Default is null or "" ? "x" : info.Default + "x";
                break;
        }
    }

    static List<CorpusFile> Resolve(string glob) {
        var files = new List<CorpusFile>();
        foreach (var set in new[] { Corpus.Constructs, Corpus.Real, Corpus.Pathological }) {
            var prefix = set + "/";
            if (!glob.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var pattern = glob[prefix.Length..];
            files.AddRange(Corpus.Files(set).Where(file => Matches(file.RelativePath, pattern)));
        }

        return files;
    }

    static bool Matches(string path, string pattern) {
        if (!pattern.Contains('*', StringComparison.Ordinal)) {
            return string.Equals(path, pattern, StringComparison.Ordinal);
        }

        var parts = pattern.Split('*');
        var cursor = 0;
        for (var i = 0; i < parts.Length; i++) {
            if (parts[i].Length == 0) {
                continue;
            }

            var index = path.IndexOf(parts[i], cursor, StringComparison.Ordinal);
            if (index < 0 || i == 0 && index != 0) {
                return false;
            }

            cursor = index + parts[i].Length;
        }

        return parts[^1].Length == 0 || path.EndsWith(parts[^1], StringComparison.Ordinal);
    }
}
