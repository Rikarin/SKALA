using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     docs/plan/09's lifecycle: the fingerprint, the baseline, the new-code definition and the gate.
/// </summary>
/// <remarks>
///     ⚠ These are the tests that decide whether the analysis half is adoptable. A rule that
///     over-fires costs a team some triage; a fingerprint that moves costs them the baseline, every
///     commit, permanently — and the failure is silent, because a baseline that matches nothing looks
///     exactly like a repository where everything is new.
/// </remarks>
public sealed class LifecycleTests {
    static readonly string Root = Path.GetFullPath("/tmp/repo");

    static Finding Finding(
        string ruleId = "SK1010",
        int line = 12,
        int start = 300,
        string symbol = "Vixen.Core.Foo.Bar(int, string)",
        string snippet = "source != null",
        string file = "Core/Foo.cs",
        SkalaSeverity severity = SkalaSeverity.Info
    ) =>
        new() {
            RuleId = ruleId,
            Severity = severity,
            Message = "Use `is not null` instead of `!= null`",
            Path = Path.Combine(Root, file.Replace('/', Path.DirectorySeparatorChar)),
            Line = line,
            Column = 9,
            EndLine = line,
            EndColumn = 24,
            Start = start,
            Length = 15,
            EnclosingSymbol = symbol,
            Snippet = snippet
        };

    static RunReport Report(params Finding[] findings) =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            Findings = Fingerprints.Assign([.. findings]),
            ConfigurationFingerprint = "abcdef0123456789",
            Duration = TimeSpan.FromSeconds(1)
        };

    /// <summary>A finding the formatter reports: no snippet and no enclosing symbol, identified by its message.</summary>
    static Finding LongLine(string file, int start, int columns = 124) =>
        Finding("SK0002", file: file, start: start) with {
            EnclosingSymbol = string.Empty,
            Snippet = string.Empty,
            Message = $"the line is {columns} columns and nothing in it could break"
        };

    /// <summary>
    ///     Rewrites a baseline the current writer produced into the shape M6 wrote: each named v3 hash
    ///     becomes a <c>skala/v2</c> entry holding the given value, and no <c>skala/v3</c> key remains.
    /// </summary>
    static string AsWrittenByM6(string sarif, params (string V3, string LegacyV2)[] entries) {
        foreach (var (v3, legacy) in entries) {
            var before = sarif;
            sarif = sarif.Replace(
                $"\"{Fingerprints.Version3}\": \"{v3}\"",
                $"\"{Fingerprints.Version2}\": \"{legacy}\"",
                StringComparison.Ordinal
            );
            Assert.NotEqual(before, sarif);
        }

        Assert.DoesNotContain(Fingerprints.Version3, sarif, StringComparison.Ordinal);
        return sarif;
    }

    // ---------------------------------------------------------------- the fingerprint

    /// <summary>
    ///     ⚠ The property the whole baseline mechanism rests on.
    /// </summary>
    /// <remarks>
    ///     doc 09: "No line numbers. A fingerprint that moves when a line moves is a baseline that
    ///     expires every commit."
    /// </remarks>
    [Fact]
    public void FingerprintV3_SurvivesTheFindingMovingDownTheFile() =>
        Assert.Equal(
            Fingerprints.V3(Finding(line: 12, start: 300)),
            Fingerprints.V3(Finding(line: 4801, start: 191_204))
        );

    /// <summary>⚠ And a file being renamed, which is the other half of "stable across file moves".</summary>
    [Fact]
    public void FingerprintV3_SurvivesTheFileBeingRenamed() =>
        Assert.Equal(
            Fingerprints.V3(Finding(file: "Core/Foo.cs")),
            Fingerprints.V3(Finding(file: "Engine/Renamed/Foo.cs"))
        );

    /// <summary>
    ///     ⚠ <c>SK7020</c> names a paired occurrence in its message, but that occurrence's lines are
    ///     no more part of this finding's identity than its own <see cref="Finding.Line" /> is.
    /// </summary>
    [Fact]
    public void FingerprintV3_OfADuplicatedBlockSurvivesThePairedCloneMovingDownTheFile() {
        var before = Finding("SK7020", snippet: string.Empty) with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/Program.cs:1003-1035"
        };
        var after = before with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/Program.cs:1029-1061"
        };

        Assert.Equal(Fingerprints.V3(before), Fingerprints.V3(after));
    }

    /// <summary>⚠ The paired file path is display text too, not fingerprint identity.</summary>
    [Fact]
    public void FingerprintV3_OfADuplicatedBlockSurvivesThePairedFileBeingRenamed() {
        var before = Finding("SK7020", snippet: string.Empty) with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/Program.cs:1003-1035"
        };
        var after = before with {
            Message = "duplicated block of 128 tokens (40 lines), also at Tests/RenamedProgram.cs:1003-1035"
        };

        Assert.Equal(Fingerprints.V3(before), Fingerprints.V3(after));
    }

    [Fact]
    public void FingerprintV3_DiffersWhenTheEnclosingSymbolDoes() =>
        Assert.NotEqual(
            Fingerprints.V3(Finding(symbol: "Vixen.Core.Foo.Bar(int, string)")),
            Fingerprints.V3(Finding(symbol: "Vixen.Core.Foo.Baz(int, string)"))
        );

    [Fact]
    public void FingerprintV3_DiffersWhenTheSnippetDoes() =>
        Assert.NotEqual(Fingerprints.V3(Finding(snippet: "a != null")), Fingerprints.V3(Finding(snippet: "b != null")));

    /// <summary>
    ///     ⚠ Two identical findings in one method are two findings.
    /// </summary>
    /// <remarks>
    ///     Without the ordinal they share a fingerprint, and a baseline that accepts one accepts both —
    ///     so fixing one of them silently keeps the other suppressed forever.
    /// </remarks>
    [Fact]
    public void Ordinal_SeparatesTwoIdenticalFindingsInOneSymbol() {
        var report = Report(Finding(line: 10, start: 100), Finding(line: 20, start: 200));

        Assert.Equal([0, 1], report.Findings.Select(static f => f.OrdinalWithinSymbol).Order());
        Assert.NotEqual(Fingerprints.V3(report.Findings[0]), Fingerprints.V3(report.Findings[1]));
    }

    /// <summary>
    ///     ⚠ A finding with no snippet is still its own finding, and its identity is its message.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="Fingerprints.V3" /> hashes <c>Snippet</c> when there is one and <c>Message</c>
    ///         when there is not, but <see cref="Fingerprints.Assign" /> keyed its counter on
    ///         <c>Snippet</c> alone — so for every rule that reports without a snippet the third term of
    ///         the key was the empty string for all of them, they landed in one group, and the "ordinal
    ///         within symbol" became each finding's index in the run's whole path-ordered list of that
    ///         rule. <c>SK7020</c> is such a rule, and on this repository its 53 findings held the
    ///         ordinals 0 to 52 with no repeats: proof that the group had collapsed, because two
    ///         genuinely identical findings are what an ordinal is for.
    ///     </para>
    ///     <para>
    ///         ⚠ The consequence is the one doc 09 calls the silent failure. Insert or remove one
    ///         duplication anywhere above another in path order and every later ordinal shifts by one,
    ///         so every later fingerprint changes: the baseline stops matching findings it already
    ///         accepted, <c>prune</c> reads them as fixed and deletes them, and the same findings come
    ///         back as new on the next run. That is the whole of the 36-finding self-gate failure —
    ///         35 <c>SK7020</c> and one <c>SK7001</c>, none of them new code.
    ///     </para>
    ///     <para>
    ///         ⚠ <see cref="Fingerprints.Assign" />'s own summary states the invariant this broke: "the
    ///         group key is everything the fingerprint uses <em>except</em> the ordinal". It was a true
    ///         sentence about a key that did not implement it.
    ///     </para>
    /// </remarks>
    [Fact]
    public void FingerprintV3_OfASnippetlessFindingSurvivesAnotherAppearingAboveIt() {
        var subject = Finding("SK7020", file: "Zed/Last.cs", start: 900) with {
            Snippet = string.Empty, Message = "duplicated block of 131 tokens (102 lines), also at A.cs:12-32"
        };

        var unrelated = Finding("SK7020", file: "Aaa/First.cs", start: 100) with {
            Snippet = string.Empty, Message = "duplicated block of 9 tokens (2 lines), also at B.cs:1-2"
        };

        var alone = Report(subject).Findings.Single();
        var crowded = Report(unrelated, subject).Findings.Single(static finding => finding.Start == 900);

        // Two different findings, so each is the first of its own kind — not 0 and 1.
        Assert.Equal(0, crowded.OrdinalWithinSymbol);
        Assert.Equal(Fingerprints.V3(alone), Fingerprints.V3(crowded));
    }

    /// <summary>
    ///     ⚠ And the ordinal still does its job: two findings that really are identical stay apart.
    /// </summary>
    [Fact]
    public void Ordinal_StillSeparatesTwoIdenticalSnippetlessFindings() {
        var one = Finding("SK7020", file: "Core/Foo.cs", start: 100) with {
            Snippet = string.Empty, Message = "duplicated block of 9 tokens (2 lines), also at B.cs:1-2"
        };

        var two = one with { Start = 200 };
        var report = Report(one, two);

        Assert.Equal([0, 1], report.Findings.Select(static f => f.OrdinalWithinSymbol).Order());
        Assert.NotEqual(Fingerprints.V3(report.Findings[0]), Fingerprints.V3(report.Findings[1]));
    }

    /// <summary>
    ///     ⚠ The ordinal is assigned by position, not by the order the analyzers happened to finish in.
    /// </summary>
    /// <remarks>
    ///     Analyzers run concurrently (doc 07 § "Parallelism"). If the ordinal followed arrival order,
    ///     the same tree would fingerprint differently between two runs and the baseline would expire
    ///     at random.
    /// </remarks>
    [Fact]
    public void Ordinal_IsIndependentOfTheOrderFindingsArriveIn() {
        var forwards = Report(Finding(line: 10, start: 100), Finding(line: 20, start: 200));
        var backwards = Report(Finding(line: 20, start: 200), Finding(line: 10, start: 100));

        Assert.Equal(
            forwards.Findings.Select(Fingerprints.V3).Order(),
            backwards.Findings.Select(Fingerprints.V3).Order()
        );
    }

    /// <summary>
    ///     ⚠ #365, as it happened: a symbol-less finding in a file the commit never touched keeps its
    ///     fingerprint when an identical one appears in a file that sorts before it.
    /// </summary>
    /// <remarks>
    ///     <c>SK0002</c> reports at column 1 with no enclosing symbol and no snippet, so v2 had no
    ///     location term for it at all and its "ordinal within symbol" was its index among every
    ///     like-worded long line in the run, in path order. #363 added one 124-column line under
    ///     <c>Analysis/</c>; <c>Tools/…/McpServerTests.cs:249</c> went from ordinal 15 to 16, the
    ///     self-gate reported the untouched Mcp test as <b>new</b>, and the actually-new line was
    ///     accepted under the Mcp test's baseline entry. Under v3 the counter never reaches outside the
    ///     file, so the subject stays at ordinal 0 whatever <c>Analysis/</c> gains.
    /// </remarks>
    [Fact]
    public void FingerprintV3_OfASymbolLessFindingSurvivesAnotherAppearingInAnEarlierFile() {
        var subject = LongLine("Tools/Rikarin.Skala.Mcp.Tests/McpServerTests.cs", 9000);
        var unrelated = LongLine("Analysis/Rikarin.Skala.Analysis.Tests/CrashedRunCacheTests.cs", 100);

        var alone = Report(subject).Findings.Single();
        var crowded = Report(unrelated, subject).Findings.Single(static finding => finding.Start == 9000);

        Assert.Equal(0, crowded.OrdinalWithinSymbol);
        Assert.Equal(Fingerprints.V3(alone), Fingerprints.V3(crowded));
    }

    /// <summary>
    ///     ⚠ Within one file the ordinal still separates two identical symbol-less findings, and it is a
    ///     position in offset order, not an offset — a line inserted above both moves neither ordinal.
    /// </summary>
    /// <remarks>
    ///     <see cref="Fingerprints.Assign" />: "within a group the order is by path and then by offset".
    ///     An ordinal that followed the offset would be a line number by another name, which is the one
    ///     thing doc 09 says the fingerprint must not contain.
    /// </remarks>
    [Fact]
    public void Ordinal_OfSymbolLessFindingsIsAPositionWithinTheFile() {
        var first = LongLine("Core/Foo.cs", 100);
        var second = LongLine("Core/Foo.cs", 900);

        var before = Report(first, second).Findings;
        var after = Report(first with { Start = 140 }, second with { Start = 940 }).Findings;

        Assert.Equal([0, 1], before.Select(static f => f.OrdinalWithinSymbol));
        Assert.NotEqual(Fingerprints.V3(before[0]), Fingerprints.V3(before[1]));
        Assert.Equal(before.Select(Fingerprints.V3), after.Select(Fingerprints.V3));
    }

    /// <summary>
    ///     ⚠ Two same-named files in different directories do not share a symbol-less finding's identity,
    ///     even though the fingerprint carries the file name and not the path.
    /// </summary>
    /// <remarks>
    ///     The counter is keyed on the same file-name term the hash uses, so the two findings are
    ///     ordinals 0 and 1 of one group rather than two ordinal-0 findings with one hash — which is what
    ///     keying the counter on the full path while hashing the name would have produced, and a baseline
    ///     accepting one would then have accepted the other. What remains is that a same-named file
    ///     gaining the same finding renumbers this one; the file-name term confines #365's failure to
    ///     that case rather than removing it, and the enclosing symbol is what removes it where one exists.
    /// </remarks>
    [Fact]
    public void FingerprintV3_OfSymbolLessFindingsInSameNamedFilesStayApart() {
        var report = Report(LongLine("Tools/Program.cs", 100), LongLine("Web/Program.cs", 100));

        Assert.Equal([0, 1], report.Findings.Select(static f => f.OrdinalWithinSymbol).Order());
        Assert.NotEqual(Fingerprints.V3(report.Findings[0]), Fingerprints.V3(report.Findings[1]));
    }

    /// <summary>⚠ And a symbol-less finding keeps the one stability the symbol would have given it: a directory move.</summary>
    [Fact]
    public void FingerprintV3_OfASymbolLessFindingSurvivesItsFileMovingDirectories() =>
        Assert.Equal(
            Fingerprints.V3(Report(LongLine("Core/Foo.cs", 100)).Findings.Single()),
            Fingerprints.V3(Report(LongLine("Engine/Moved/Foo.cs", 100)).Findings.Single())
        );

    /// <summary>
    ///     ⚠ v1 is still emitted beside v3, so a baseline written before M6 still reads — and v2 is not,
    ///     because a <c>skala/v2</c> computed from a file-scoped ordinal would be a second meaning under
    ///     M6's key.
    /// </summary>
    [Fact]
    public void FingerprintVersions_V1AndV3AreEmittedAndV2IsNot() {
        var prints = Fingerprints.For(Finding());
        Assert.Equal(Fingerprints.V1(Finding()), prints[Fingerprints.Version1]);
        Assert.Equal(Fingerprints.V3(Finding()), prints[Fingerprints.Version3]);
        Assert.NotEqual(prints[Fingerprints.Version1], prints[Fingerprints.Version3]);
        Assert.DoesNotContain(Fingerprints.Version2, prints.Keys);
    }

    // ---------------------------------------------------------------- the baseline

    [Fact]
    public void Baseline_RoundTripsThroughSarifAndMatchesNothingAsNew() {
        var report = Report(Finding(), Finding("SK1030", 40, 900, snippet: "x = x ?? y"));
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, report, report.Findings);
            var comparison = Baseline.Read(path).Compare(report.Findings);

            Assert.Equal(0, comparison.NewCount);
            Assert.Empty(comparison.Fixed);
            Assert.All(comparison.Findings, static f => Assert.Equal(BaselineBucket.Existing, f.Bucket));
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     ⚠ A baseline M6 wrote still matches, run-wide ordinal and all, until it is rewritten.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The two hashes are copied from this repository's committed <c>.skala/baseline.sarif</c> as
    ///         it stood before #365: the <c>SK0002</c> "121 columns" entries with no
    ///         <c>ordinalWithinSymbol</c> (ordinal 0) and with ordinal 10. They were written by the old
    ///         code, so this pins <see cref="Fingerprints.LegacyV2" /> to what M6 actually produced rather
    ///         than to itself — and the ordinal-10 anchor only matches if the legacy numbering is still
    ///         counted across files in path order, which is the defect the fallback has to reproduce
    ///         faithfully to keep an old baseline matching.
    ///     </para>
    ///     <para>
    ///         ⚠ The other nine entries carry hashes M6 never wrote, so their findings are new and they are
    ///         fixed: a v2-only entry is matched by v2 and by nothing weaker.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Baseline_WrittenByM6MatchesOnItsRunWideOrdinalUntilRewritten() {
        const string OrdinalZero = "72bd84df2698ac48d3e68fefc5424b65";
        const string OrdinalTen = "68cceca0e9189b307136e39c35353e8f";

        var findings = Enumerable.Range(0, 11)
            .Select(static i => LongLine($"Legacy/F{i:00}.cs", 50, 121))
            .ToArray();
        var report = Report(findings);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, report, report.Findings);
            File.WriteAllText(
                path,
                AsWrittenByM6(
                    File.ReadAllText(path),
                    [
                        .. report.Findings.Select((finding, i) => (
                                Fingerprints.V3(finding),
                                i switch {
                                    0 => OrdinalZero,
                                    10 => OrdinalTen,
                                    _ => i.ToString("d32", System.Globalization.CultureInfo.InvariantCulture)
                                }
                            )
                        )
                    ]
                )
            );

            var baseline = Baseline.Read(path);
            var comparison = baseline.Compare(report.Findings);

            Assert.Equal(9, comparison.NewCount);
            Assert.Equal(BaselineBucket.Existing, comparison.Findings[0].Bucket);
            Assert.Equal(BaselineBucket.New, comparison.Findings[5].Bucket);
            Assert.Equal(BaselineBucket.Existing, comparison.Findings[10].Bucket);
            Assert.Equal(9, comparison.Fixed.Length);
            Assert.DoesNotContain(comparison.Fixed, static entry => entry.FingerprintV2 is OrdinalZero or OrdinalTen);

            // Rewritten by the current code, the same run matches on v3 with no legacy pass.
            Baseline.Write(path, report, report.Findings);
            var rewritten = Baseline.Read(path);
            Assert.All(rewritten.Entries, static entry => Assert.Empty(entry.FingerprintV2));
            Assert.Equal(0, rewritten.Compare(report.Findings).NewCount);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     ⚠ Baselines already committed before SK7020's identity was corrected carry its volatile
    ///     v2 hash. Reading one must recover the stable identity from the SARIF fields, or adopting the
    ///     fix itself makes every accepted duplicated block new once.
    /// </summary>
    [Fact]
    public void Baseline_LegacyDuplicatedBlockFingerprintMatchesAfterThePairedCloneMoves() {
        var accepted = Finding("SK7020", snippet: string.Empty) with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/Program.cs:1003-1035"
        };
        var report = Report(accepted);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, report, report.Findings);
            File.WriteAllText(
                path,
                AsWrittenByM6(
                    File.ReadAllText(path),
                    (Fingerprints.V3(report.Findings.Single()), "00000000000000000000000000000000")
                )
            );

            var moved = Report(
                accepted with {
                    Message = "duplicated block of 128 tokens (40 lines), also at Testing/Program.cs:1029-1061"
                }
            );
            var comparison = Baseline.Read(path).Compare(moved.Findings);

            Assert.Equal(0, comparison.NewCount);
            Assert.Empty(comparison.Fixed);
            Assert.Equal(BaselineBucket.Existing, comparison.Findings.Single().Bucket);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     ⚠ Removing the related location reveals legitimate identity collisions: two different
    ///     clone groups can have the same token and line counts. Legacy entries gave both ordinal zero,
    ///     so migration must assign the same path-ordered ordinals as a fresh run.
    /// </summary>
    [Fact]
    public void Baseline_LegacyDuplicatedBlockCollisionsRecoverTheirStableOrdinals() {
        var first = Finding("SK7020", start: 100, snippet: string.Empty) with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/First.cs:1003-1035"
        };
        var second = Finding("SK7020", start: 200, snippet: string.Empty) with {
            Message = "duplicated block of 128 tokens (40 lines), also at Testing/Second.cs:2003-2035"
        };
        var accepted = Report(first, second);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, accepted, accepted.Findings);
            File.WriteAllText(
                path,
                AsWrittenByM6(
                    File.ReadAllText(path),
                    (Fingerprints.V3(accepted.Findings[0]), "00000000000000000000000000000000"),
                    (Fingerprints.V3(accepted.Findings[1]), "11111111111111111111111111111111")
                )
            );

            var moved = Report(
                first with {
                    Message = "duplicated block of 128 tokens (40 lines), also at Testing/First.cs:1029-1061"
                },
                second with {
                    Message = "duplicated block of 128 tokens (40 lines), also at Testing/Second.cs:2029-2061"
                }
            );
            var comparison = Baseline.Read(path).Compare(moved.Findings);

            Assert.Equal(0, comparison.NewCount);
            Assert.Empty(comparison.Fixed);
            Assert.All(comparison.Findings, static finding => Assert.Equal(BaselineBucket.Existing, finding.Bucket));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Baseline_PartitionsIntoNewExistingAndFixed() {
        var accepted = Report(Finding(), Finding("SK1030", 40, 900, snippet: "x = x ?? y"));
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, accepted, accepted.Findings);

            // One of the two still fires; a third has appeared.
            var now = Report(Finding(), Finding("SK1034", 70, 1500, snippet: "items.Count() > 0"));
            var comparison = Baseline.Read(path).Compare(now.Findings);

            Assert.Equal(1, comparison.NewCount);
            Assert.Single(comparison.Fixed);
            Assert.Equal("SK1030", comparison.Fixed[0].RuleId);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>⚠ An absent baseline is empty; an unreadable one throws. See <see cref="Baseline.Read" />.</summary>
    [Fact]
    public void Baseline_AbsentIsEmptyAndCorruptThrows() {
        Assert.Equal(0, Baseline.Read(Path.Combine(Path.GetTempPath(), "nothing-here.sarif")).Count);

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");
        try {
            File.WriteAllText(path, "{ this is not sarif");
            Assert.ThrowsAny<Exception>(() => Baseline.Read(path));
        } finally {
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------- the gate

    /// <summary>
    ///     ⚠ <c>newIssues</c> with nothing to define "new" is a configuration error, not a pass.
    /// </summary>
    /// <remarks>
    ///     Counting every finding in the repository as new would make <c>newIssues: 0</c> mean "the
    ///     repository is perfect", which nobody who wrote it meant.
    /// </remarks>
    [Fact]
    public void Gate_NewIssuesWithoutABaselineOrSince_Fails() {
        var result = Gate.Evaluate(
            new GateDefinition { Name = "ci", MaxNewIssues = 0 },
            Report(Finding()),
            true
        );

        Assert.False(result.Passed);
        Assert.Contains(
            result.Failures,
            static f => f.Contains("needs a baseline or --since", StringComparison.Ordinal)
        );
    }

    /// <summary>
    ///     ⚠ With a baseline in play, <c>maxSeverity</c> is about the new findings.
    /// </summary>
    /// <remarks>
    ///     Read literally, doc 09's own `ci` gate — a baseline plus `maxSeverity: warning` — is
    ///     unsatisfiable on any repository that has ever had a warning, which contradicts the
    ///     adoption story § "New-code definition" is built on. Measured on Vixen's Core: 994 accepted,
    ///     0 new, and a literal reading still failing on 308 of the accepted ones.
    /// </remarks>
    [Fact]
    public void Gate_MaxSeverityIsScopedToNewFindingsWhenABaselineIsInPlay() {
        var warning = Finding(severity: SkalaSeverity.Warning);
        var definition = new GateDefinition { Name = "ci", MaxSeverity = SkalaSeverity.Warning };

        var unscoped = Report(warning);
        Assert.False(Gate.Evaluate(definition, unscoped, true).Passed);

        var accepted = unscoped with {
            HasBaseline = true,
            Findings = [.. unscoped.Findings.Select(static f => f with { Bucket = BaselineBucket.Existing })]
        };

        Assert.True(Gate.Evaluate(definition, accepted, true).Passed);
    }

    /// <summary>⚠ "New" is the intersection of the scopings, never the union.</summary>
    [Fact]
    public void New_IsTheIntersectionOfBaselineAndSince() {
        var outside = Finding(line: 10, start: 100) with { Bucket = BaselineBucket.New, IsInChangedCode = false };
        var inside = Finding(line: 20, start: 200) with { Bucket = BaselineBucket.New, IsInChangedCode = true };
        var accepted = Finding(line: 30, start: 300) with { Bucket = BaselineBucket.Existing, IsInChangedCode = true };

        var report = Report(outside, inside, accepted) with {
            HasBaseline = true, ChangedCodeReference = "origin/main", Findings = [outside, inside, accepted]
        };

        Assert.Single(report.New);
        Assert.Equal(20, report.New.Single().Line);
    }

    /// <summary>⚠ A metric a gate names and the run did not measure fails, rather than passing.</summary>
    [Fact]
    public void Gate_AMetricThatWasNotMeasured_Fails() {
        var definition = new GateDefinition {
            Name = "ci", Metrics = ImmutableDictionary<string, double>.Empty.Add("duplication", 3.0)
        };

        var result = Gate.Evaluate(definition, Report(Finding()), true);
        Assert.False(result.Passed);
        Assert.Contains(result.Failures, static f => f.Contains("was not measured", StringComparison.Ordinal));
    }

    [Fact]
    public void Gate_AnUnknownMetricName_Fails() {
        var definition = new GateDefinition {
            Name = "ci", Metrics = ImmutableDictionary<string, double>.Empty.Add("coverage", 80)
        };

        Assert.False(Gate.Evaluate(definition, Report(Finding()), true).Passed);
    }

    /// <summary>⚠ <c>commentDensity</c> is a floor; everything else is a ceiling.</summary>
    [Fact]
    public void Gate_CommentDensityIsAFloorAndDuplicationIsACeiling() {
        var metrics = new MetricsSummary {
            MemberCount = 100,
            TotalLines = 1000,
            DuplicatedLines = 20,
            Duplication = 2.0,
            CommentDensity = 40
        };

        var report = Report(Finding()) with { Metrics = metrics };

        Assert.True(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", Metrics = ImmutableDictionary<string, double>.Empty.Add("duplication", 3.0)
                },
                report,
                true
            ).Passed
        );

        Assert.False(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", Metrics = ImmutableDictionary<string, double>.Empty.Add("commentDensity", 60)
                },
                report,
                true
            ).Passed
        );
    }

    [Fact]
    public void Gate_RuleOverridesMatchAPrefixGlobAndAnExactId() {
        var report = Report(Finding("SK5001"), Finding("SK1010", 40, 900));

        Assert.False(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", RuleOverrides = ImmutableDictionary<string, int>.Empty.Add("SK5*", 0)
                },
                report,
                true
            ).Passed
        );

        Assert.True(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", RuleOverrides = ImmutableDictionary<string, int>.Empty.Add("SK9001", 0)
                },
                report,
                true
            ).Passed
        );
    }

    /// <summary>⚠ A condition this build does not understand fails the gate rather than being ignored.</summary>
    [Fact]
    public void Gate_AnUnsupportedCondition_FailsRatherThanBeingDropped() {
        var result = Gate.Evaluate(
            new GateDefinition { Name = "ci", Unsupported = ["coverage"] },
            Report(),
            true
        );

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, static f => f.Contains("coverage", StringComparison.Ordinal));
    }

    /// <summary>
    ///     ⚠ Doc 09's exit codes are a contract hooks, CI and agents depend on. 2 is distinct from 1.
    /// </summary>
    [Fact]
    public void ExitCodes_AreTheDocumentedValues() {
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(1, ExitCodes.GateFailed);
        Assert.Equal(2, ExitCodes.FormattingNeeded);
        Assert.Equal(3, ExitCodes.ConfigurationError);
        Assert.Equal(4, ExitCodes.LoadFailure);
        Assert.Equal(5, ExitCodes.InternalError);
        Assert.Equal(130, ExitCodes.Cancelled);
        Assert.NotEqual(ExitCodes.GateFailed, ExitCodes.FormattingNeeded);
    }

    // ---------------------------------------------------------------- suppressions

    /// <summary>⚠ "Did not audit" and "audited and found nothing" are different facts.</summary>
    [Fact]
    public void SuppressionAudit_OffDoesNotFailTheGate() {
        Assert.False(SuppressionAudit.Off.Enforced);
        Assert.True(Gate.Evaluate(GateDefinition.Local, Report(), true).Passed);
    }

    [Fact]
    public void Gate_ANewSuppression_Fails() {
        var report = Report() with {
            Suppressions = new() {
                Enforced = true,
                Reference = "origin/main",
                Added = [new SuppressionEntry(SuppressionSource.EditorConfig, "SK3002", ".editorconfig [*.cs]", "none")]
            }
        };

        var result = Gate.Evaluate(GateDefinition.Local, report, true);
        Assert.False(result.Passed);

        // ⚠ The .editorconfig form specifically: a grep for `#pragma` is not a constraint.
        Assert.Contains(result.Failures, static f => f.Contains(".editorconfig", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- history

    [Fact]
    public void History_RoundTripsAndSkipsATornLine() {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        try {
            History.Append(directory, History.Entry(Report(Finding()), "abc1234", "main"));
            File.AppendAllText(History.PathFor(directory), "{ not json\n");
            History.Append(directory, History.Entry(Report(Finding()), "def5678", "main"));

            var entries = History.Read(directory);
            Assert.Equal(2, entries.Length);
            Assert.Equal("abc1234", entries[0].Sha);
            Assert.Equal("def5678", entries[1].Sha);
            Assert.Contains("findings", History.Render(entries, 20), StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    // ---------------------------------------------------------------- percentiles

    /// <summary>⚠ Nearest-rank, so every reported number is some member's actual score.</summary>
    [Fact]
    public void Percentile_IsNearestRankAndNeverInterpolates() {
        int[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(10, MetricsSummary.Percentile(sorted, 0.95));
        Assert.Equal(5, MetricsSummary.Percentile(sorted, 0.5));
        Assert.Equal(0, MetricsSummary.Percentile([], 0.95));
    }
}
