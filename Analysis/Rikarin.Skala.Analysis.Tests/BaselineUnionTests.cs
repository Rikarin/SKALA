using Newtonsoft.Json.Linq;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #366: <c>skala baseline update</c> keeps an entry whose finding no longer fires by writing the
///     entry it read, not a placeholder hashed in its place.
/// </summary>
/// <remarks>
///     <para>
///         Measured through the real binary before the fix, over the loose path. A three-finding
///         baseline had its <c>SK1030</c> fixed and <c>update --apply</c> run: the kept entry came back
///         with <c>skala/v3</c> <c>62411b77…</c> where it had been <c>af4153e1…</c>, <c>startLine 1</c>
///         and <c>charOffset 0</c> where it had been line 8 offset 163, no <c>enclosingSymbol</c>, and
///         <c>skalaSeverity</c> <c>hint</c> where it had been <c>suggestion</c>. Un-fixing the line and
///         running <c>check --baseline</c> reported it <b>new</b>, and <c>baseline show</c> counted the
///         ghost as <em>fixed</em> beside it — docs/plan/09's "reported as good news and pruned only
///         when asked" was holding for an entry that no longer described anything.
///     </para>
///     <para>
///         ⚠ <b>What fingerprint v3 (#365) did to the alias the issue describes.</b> Under v2 a
///         symbol-less placeholder hashed to <c>(rule, message, "", 0)</c>, which is the run-wide
///         ordinal-0 finding of that message in <em>any</em> file; two <c>SK0002</c> ghosts in the
///         committed baseline were "seen" that way. v3 hashes the file name for a symbol-less finding,
///         so the same placeholder now hashes to the ordinal-0 identity of its own rule-message-file
///         group — which for an ordinal-0 entry is its own original hash, and for a higher ordinal is
///         a sibling already in the file. Measured over two same-named files: fixing <c>one/Program.cs</c>
///         renumbered <c>two/Program.cs</c> to ordinal 0 (the residual #365 documents), the unfired
///         ordinal-1 entry was rehashed to that same ordinal-0 value, and the written file held two
///         entries with one <c>skala/v3</c> while <c>prune</c> reported <c>0 … would be removed</c>.
///         So v3 narrowed the alias from "any file" to "a duplicate of an accepted hash that
///         <c>prune</c> cannot see"; it did not remove the mechanism, and it did nothing for the
///         symbol-bearing entries, which lose their identity outright.
///     </para>
///     <para>
///         ⚠ <b>Sabotage:</b> put the placeholder back in <c>BaselineCommand</c> — a <c>Finding</c> per
///         <c>comparison.Fixed</c> entry with an empty symbol and snippet, passed to the three-argument
///         <c>Baseline.Write</c> — and <see cref="Update_KeepsAnUnfiredEntryByteForByte" />,
///         <see cref="Update_ThenTheFindingReturns_IsExisting" />,
///         <see cref="Update_OverSameNamedFiles_DoesNotRehashTheUnfiredEntryOntoItsNeighbour" /> and
///         <see cref="Update_CarriesAV2OnlyEntryAsV2Only_AndItStillMatches" /> all go red.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class BaselineUnionTests {
    /// <summary>
    ///     A finding with an enclosing symbol — the shape two thirds of this repository's baseline has,
    ///     and the one the placeholder loses outright.
    /// </summary>
    const string Unfixed = """
                           namespace Scratch {
                               using System.Collections.Generic;

                               public sealed class Holder {
                                   List<int>? _items;

                                   public void Ensure() {
                                       _items = _items ?? new List<int>();
                                   }
                               }
                           }
                           """;

    const string Fixed = """
                         namespace Scratch {
                             using System.Collections.Generic;

                             public sealed class Holder {
                                 List<int>? _items;

                                 public void Ensure() {
                                     _items ??= new List<int>();
                                 }
                             }
                         }
                         """;

    /// <summary>
    ///     A line with no break point in it — <c>SK0002</c>, symbol-less, at column 1. The literal is
    ///     sized so that the line is exactly 121 columns: see <see cref="LegacyV2OfThe121ColumnLine" />.
    /// </summary>
    const string LongLine = """
                            namespace Scratch;

                            public static class Wide {
                                public const string Reference =
                                    "https://example.invalid/a-very-long-path-segment-does-not-contain-anything-the-formatter-could-break-on-at-all";
                            }

                            """;

    const string ShortLine = """
                             namespace Scratch;

                             public static class Wide {
                                 public const string Reference = "short";
                             }

                             """;

    /// <summary>
    ///     ⚠ M6's <c>skala/v2</c> for <c>SK0002</c> "the line is 121 columns and nothing in it could
    ///     break" at run-wide ordinal 0, copied from this repository's committed baseline as it stood
    ///     before #365 — the same anchor <c>LifecycleTests</c> pins the legacy pass to. It is what the
    ///     old code actually wrote, so the v2 carry below is checked against a real pre-v3 entry rather
    ///     than against a reimplementation of the hash.
    /// </summary>
    const string LegacyV2OfThe121ColumnLine = "72bd84df2698ac48d3e68fefc5424b65";

    static CheckRequest Request(Scratch scratch, string baseline) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            NoCache = true,
            BaselinePath = baseline
        };

    static string BaselinePath(Scratch scratch) => Path.Combine(scratch.Root, ".skala", "baseline.sarif");

    static CommandResult Baseline(BaselineCommand.Verb verb, Scratch scratch) {
        var (result, _) = BaselineCommand.Run(
            verb,
            Request(scratch, BaselinePath(scratch)),
            true,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.Contains("  written.", result.Output, StringComparison.Ordinal);
        return result;
    }

    static RunReport Check(Scratch scratch) {
        var (_, report) = CheckCommand.Run(
            Request(scratch, BaselinePath(scratch)),
            TestContext.Current.CancellationToken
        );
        Assert.True(report.HasBaseline, "the check must have compared against the baseline");
        return report;
    }

    /// <summary>The <c>results[]</c> elements of a baseline file, keyed by rule id, as JSON.</summary>
    static Dictionary<string, JObject> Results(string path) =>
        ((JArray)JObject.Parse(File.ReadAllText(path))["runs"]![0]!["results"]!)
            .Cast<JObject>()
            .GroupBy(static result => (string)result["ruleId"]!)
            .ToDictionary(static group => group.Key, static group => group.Single(), StringComparer.Ordinal);

    static string V3(JObject result) => (string)result["partialFingerprints"]![Fingerprints.Version3]!;

    /// <summary>
    ///     The entry written for a finding that stopped firing is the entry that was read: same
    ///     fingerprints, same region, same properties, same everything.
    /// </summary>
    [Fact]
    public void Update_KeepsAnUnfiredEntryByteForByte() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", Unfixed);
        Baseline(BaselineCommand.Verb.Create, scratch);

        var before = Results(BaselinePath(scratch));
        var accepted = before["SK1030"];

        // The premise: the entry has the shape the placeholder cannot reproduce.
        Assert.Equal("Scratch.Holder.Ensure()", (string?)accepted["properties"]?["enclosingSymbol"]);
        Assert.NotEqual(1, (int)accepted["locations"]![0]!["physicalLocation"]!["region"]!["startLine"]!);

        scratch.Write("Holder.cs", Fixed);
        var update = Baseline(BaselineCommand.Verb.Update, scratch);
        Assert.Contains(
            "1 accepted finding(s) no longer fire and are being kept",
            update.Output,
            StringComparison.Ordinal
        );

        var after = Results(BaselinePath(scratch));
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(accepted.ToString(), after["SK1030"].ToString());
    }

    /// <summary>
    ///     docs/plan/09: a kept entry is "reported as good news and pruned only when asked". The half of
    ///     that sentence the placeholder broke is that it is still the <em>same</em> entry — so the
    ///     finding, when it comes back, is existing and not new.
    /// </summary>
    [Fact]
    public void Update_ThenTheFindingReturns_IsExisting() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", Unfixed);
        Baseline(BaselineCommand.Verb.Create, scratch);

        scratch.Write("Holder.cs", Fixed);
        Baseline(BaselineCommand.Verb.Update, scratch);

        scratch.Write("Holder.cs", Unfixed);
        var report = Check(scratch);

        var returned = Assert.Single(report.Findings, static finding => finding.RuleId == "SK1030");
        Assert.Equal(BaselineBucket.Existing, returned.Bucket);
        Assert.Empty(report.New);
        Assert.Empty(report.Fixed);
    }

    /// <summary>
    ///     The alias, in the form v3 leaves it. Two same-named files share one ordinal counter, so fixing
    ///     the ordinal-0 one renumbers the other onto its hash (#365's documented residual). The unfired
    ///     ordinal-1 entry must then keep its own hash — a placeholder rehashes it to ordinal 0, which
    ///     is the neighbour's, and the file holds one identity twice where <c>prune</c> sees nothing.
    /// </summary>
    [Fact]
    public void Update_OverSameNamedFiles_DoesNotRehashTheUnfiredEntryOntoItsNeighbour() {
        using var scratch = new Scratch();
        scratch.Write(Path.Combine("one", "Program.cs"), LongLine);
        scratch.Write(Path.Combine("two", "Program.cs"), LongLine);
        Baseline(BaselineCommand.Verb.Create, scratch);

        var created = Reporting.Baseline.Read(BaselinePath(scratch)).Entries;
        Assert.Equal(2, created.Length);
        var ordinalOne = Assert.Single(created, static entry => entry.Path == "two/Program.cs");
        var ordinalZero = Assert.Single(created, static entry => entry.Path == "one/Program.cs");
        Assert.NotEqual(ordinalZero.FingerprintV3, ordinalOne.FingerprintV3);

        scratch.Write(Path.Combine("one", "Program.cs"), ShortLine);
        Baseline(BaselineCommand.Verb.Update, scratch);

        var updated = Reporting.Baseline.Read(BaselinePath(scratch)).Entries;
        Assert.Equal(2, updated.Length);
        Assert.Equal(2, updated.Select(static entry => entry.FingerprintV3).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(updated, entry => entry.FingerprintV3 == ordinalOne.FingerprintV3);

        // And the ghost is visible to the verb that exists to remove it.
        var (prune, _) = BaselineCommand.Run(
            BaselineCommand.Verb.Prune,
            Request(scratch, BaselinePath(scratch)),
            false,
            TestContext.Current.CancellationToken
        );
        Assert.Contains("1 entr(y/ies) no longer fire", prune.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ An entry written before v3 carries only <c>skala/v2</c>, and an <c>update</c> that does not
    ///     see its finding fire cannot give it a v3 — there is nothing to compute one from. It must stay
    ///     v2-only, and <c>Compare</c>'s legacy pass must still match it when the finding returns. The
    ///     placeholder gave it a v3 hashed from nothing and dropped the v2, after which nothing could
    ///     ever match it again.
    /// </summary>
    [Fact]
    public void Update_CarriesAV2OnlyEntryAsV2Only_AndItStillMatches() {
        using var scratch = new Scratch();
        scratch.Write("Wide.cs", LongLine);
        Baseline(BaselineCommand.Verb.Create, scratch);

        // Rewrite the one entry into the shape M6 wrote: a v2 key holding M6's own hash, no v3.
        var path = BaselinePath(scratch);
        var written = File.ReadAllText(path);
        var v3 = V3(Results(path)["SK0002"]);
        var asWrittenByM6 = written.Replace(
            $"\"{Fingerprints.Version3}\": \"{v3}\"",
            $"\"{Fingerprints.Version2}\": \"{LegacyV2OfThe121ColumnLine}\"",
            StringComparison.Ordinal
        );
        Assert.NotEqual(written, asWrittenByM6);
        File.WriteAllText(path, asWrittenByM6);

        // The control: the anchor is a real M6 hash for this finding, so it matches before anything
        // is rewritten. If this fails the test is measuring the anchor, not the carry.
        var control = Check(scratch);
        Assert.Equal(BaselineBucket.Existing, Assert.Single(control.Findings, static f => f.RuleId == "SK0002").Bucket);

        scratch.Write("Wide.cs", ShortLine);
        Baseline(BaselineCommand.Verb.Update, scratch);

        var carried = Assert.Single(Reporting.Baseline.Read(path).Entries);
        Assert.Equal(LegacyV2OfThe121ColumnLine, carried.FingerprintV2);
        Assert.Empty(carried.FingerprintV3);

        scratch.Write("Wide.cs", LongLine);
        var report = Check(scratch);
        Assert.Equal(BaselineBucket.Existing, Assert.Single(report.Findings, static f => f.RuleId == "SK0002").Bucket);
        Assert.Empty(report.New);
        Assert.Empty(report.Fixed);
    }

    /// <summary>
    ///     <c>create</c> and <c>prune</c> write only what the run produced, so neither carries anything
    ///     and neither can mint a placeholder; pinned so that a future "keep" added to either goes
    ///     through the same path.
    /// </summary>
    [Fact]
    public void Prune_WritesOnlyWhatStillFires() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", Unfixed);
        Baseline(BaselineCommand.Verb.Create, scratch);
        var accepted = Results(BaselinePath(scratch));

        scratch.Write("Holder.cs", Fixed);
        Baseline(BaselineCommand.Verb.Update, scratch);
        Baseline(BaselineCommand.Verb.Prune, scratch);

        var pruned = Results(BaselinePath(scratch));
        Assert.DoesNotContain("SK1030", pruned.Keys);
        Assert.Equal(accepted.Count - 1, pruned.Count);
        Assert.All(pruned.Values, result => Assert.Equal(V3(accepted[(string)result["ruleId"]!]), V3(result)));
    }
}
