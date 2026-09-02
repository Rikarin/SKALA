using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Duplication;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     docs/plan/09 § "Duplication" — token-level type-2 clone detection, and <c>SK7020</c>.
/// </summary>
/// <remarks>
///     ⚠ Every assertion about a group's <c>TokenLength</c> rests on the fixture below producing blocks
///     with exactly the token count they are asked for, which is why
///     <see cref="Block_HasExactlyTheTokensItClaims" /> exists and runs first in the file.
/// </remarks>
public sealed class DuplicationTests {
    const int MinTokens = 100;

    /// <summary>Directives in <see cref="Header" />: 15 × 7 tokens, plus 5 for the namespace, is 110.</summary>
    const int HeaderDirectives = 15;

    /// <summary>⚠ One line each and one for the namespace, with no blank line — so the count is exact.</summary>
    const int HeaderLines = HeaderDirectives + 1;

    /// <summary>
    ///     Statement shapes and the exact number of tokens each lexes to, trivia dropped.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every count from 3 to 11 is present, so <see cref="Block" /> can always land on an exact
    ///     total: from any remainder of 3 or more there is a shape that consumes it or leaves 3 or more.
    /// </remarks>
    static readonly (int Tokens, string Format)[] Shapes = [
        (3, "{0}{1}++;"), (4, "{0}{1} = {0}{1};"), (5, "{0}{1} = -1;"), (6, "{0}{1} = {0}{1} + 1;"),
        (7, "{0}{1} = {2}({0}{1});"),
        (8, "{0}{1} = {0}{1} * {0}{1} - 3;"), (9, "if ({0}{1} > 1) {0}{1}++;"),
        (10, """{0}{1} = new {3}({0}{1}, "s");"""),
        (11, "while ({0}{1} < 5) {{ {0}{1}++; }}")
    ];

    [Fact]
    public void Block_HasExactlyTheTokensItClaims() {
        foreach (var tokens in (int[])[3, 4, 5, 17, 99, 100, 120, 250]) {
            Assert.Equal(tokens, TokenCount(Block(tokens)));
        }
    }

    [Fact]
    public void Detect_WhenTwoFilesShareABlock_ReportsOneGroupWithTwoOccurrences() {
        var block = Block(120);

        var result = Detect([Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block))]);

        var group = Assert.Single(result.Groups);
        Assert.Equal(120, group.TokenLength);
        Assert.Equal(2, group.Occurrences.Length);
        Assert.Equal("/repo/Alpha.cs", group.Occurrences[0].Path);
        Assert.Equal("/repo/Beta.cs", group.Occurrences[1].Path);
        Assert.True(result.Percentage > 0);
    }

    /// <summary>
    ///     ⚠ The type-2 property, and the reason the rule exists: an agent's copy-paste is a copy with the
    ///     variables renamed. If this test goes red the rule has silently become type-1 detection, which
    ///     finds almost nothing in real code.
    /// </summary>
    [Fact]
    public void Detect_WhenEveryIdentifierIsRenamed_StillReportsOneGroup() {
        var original = Block(120, "value", "Compute", "Holder");
        var renamed = Block(120, "otherName", "Evaluate", "Widget");
        Assert.NotEqual(original, renamed);

        var result = Detect(
            [Production("/repo/Alpha.cs", Alpha(original)), Production("/repo/Beta.cs", Beta(renamed))]
        );

        var group = Assert.Single(result.Groups);
        Assert.Equal(120, group.TokenLength);
        Assert.Equal(2, group.Occurrences.Length);
    }

    [Fact]
    public void Detect_WhenTheSharedBlockIsShorterThanMinTokens_ReportsNothing() {
        var block = Block(99);

        var result = Detect([Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block))]);

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.DuplicatedLines);
        Assert.Equal(0d, result.Percentage);
        Assert.True(result.TotalLines > 0);
    }

    /// <summary>
    ///     docs/plan/09 step 4, and <c>SK7020</c>'s rationale: "reporting it at every occurrence would turn
    ///     one problem into n findings and make the count meaningless".
    /// </summary>
    [Fact]
    public void Detect_WhenThreeFilesShareABlock_ReportsOneGroupAndNotThree() {
        var block = Block(120);

        var result = Detect(
            [
                Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block)),
                Production(
                    "/repo/Gamma.cs",
                    Gamma(block)
                )
            ]
        );

        var group = Assert.Single(result.Groups);
        Assert.Equal(3, group.Occurrences.Length);
        Assert.Single(CloneDetector.ToFindings(result, "/repo"));
    }

    /// <summary>
    ///     ⚠ A 250-token match contains 151 overlapping 100-token windows. Every one of them is a verified
    ///     clone class, and all 151 have to collapse into one maximal group.
    /// </summary>
    [Fact]
    public void Detect_WhenTheMatchIsLongerThanTheWindow_ExtendsToOneMaximalGroup() {
        var block = Block(250);

        var result = Detect([Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block))]);

        var group = Assert.Single(result.Groups);
        Assert.Equal(250, group.TokenLength);
        Assert.Equal(2, group.Occurrences.Length);
    }

    /// <summary>
    ///     ⚠ <b>One list is not two clones of itself</b> — issue #333.
    /// </summary>
    /// <remarks>
    ///     Every identifier normalises to one class, so a list of 60 <c>new Kind()</c> elements is 300
    ///     tokens with a period of 5 and its first hundred tokens are a verified, token-for-token clone of
    ///     its second hundred. Nothing can be extracted from it; the "duplication" is the list being a
    ///     list. This is <b>#323</b>'s file-header artefact surviving wherever a file holds a run of
    ///     similar declarations, and it was 39 of 79 open code-scanning alerts.
    ///     <para>
    ///         ⚠ The fixture has to be a real list and not a periodic token run, because the test the
    ///         detector applies is structural: one <c>CollectionExpressionSyntax</c> whose elements lex
    ///         alike at a constant stride. A block that merely repeats is still duplication.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Detect_WhenOneListMatchesItselfShifted_ReportsNothing() {
        var table = UniformList(60);
        Assert.True(TokenCount(table) > 2 * MinTokens, "the list has to hold two disjoint windows");

        var result = Detect([Production("/repo/Table.cs", table)]);

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.DuplicatedLines);
    }

    /// <summary>
    ///     ⚠ The other direction, in one corpus: the table is silent and the copy-paste beside it is not.
    /// </summary>
    /// <remarks>
    ///     ⚠ A test that only asserted the silence would pass just as well if <c>SK7020</c> had stopped
    ///     working altogether, which is the shape of failure that survives longest. The genuine pairs this
    ///     stands for are real ones: <c>PairwiseReport</c>/<c>SweepReport</c> at 163 tokens and
    ///     <c>AnalysisCommands</c>/<c>GateCommands</c> at 111, both of which a higher <c>minTokens</c>
    ///     would have silenced along with the table.
    /// </remarks>
    [Fact]
    public void Detect_WhenATableSitsBesideARealClone_ReportsOnlyTheClone() {
        var block = Block(120);

        var result = Detect(
            [
                Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block)),
                Production("/repo/Table.cs", UniformList(60))
            ]
        );

        var group = Assert.Single(result.Groups);
        Assert.Equal(120, group.TokenLength);
        Assert.Equal("/repo/Alpha.cs", group.Occurrences[0].Path);
        Assert.Equal("/repo/Beta.cs", group.Occurrences[1].Path);
        Assert.Equal(2, group.Occurrences.Length);
    }

    /// <summary>
    ///     ⚠ <b>Siblings longer than the window are still duplication</b>, and this is the assertion that
    ///     keeps the decline from swallowing them.
    /// </summary>
    /// <remarks>
    ///     Three identical method bodies in one class are three copies of a block that happen to be
    ///     siblings, and extracting them is exactly what <c>SK7020</c> is for. The detector separates them
    ///     from a table by the run's <i>period</i> and not by any threshold: a match longer than one
    ///     element spans rows, and a match that fits inside one element is a copy of that element.
    /// </remarks>
    [Fact]
    public void Detect_WhenThreeSiblingMembersAreEachLongerThanAWindow_StillReportsThem() {
        var result = Detect([Production("/repo/Holder.cs", Members(Block(120), 3))]);

        var group = Assert.Single(result.Groups);
        Assert.Equal(3, group.Occurrences.Length);
    }

    /// <summary>
    ///     <c>SK7020</c>'s <c>falsePositives</c>: "the match is verified exactly rather than trusted from
    ///     the rolling hash, so a hash collision cannot produce a finding".
    /// </summary>
    /// <remarks>
    ///     ⚠ Collapsing every window into one bucket is a worse collision than could ever occur by
    ///     accident. Unrelated files must still report nothing, and a real clone must come out identical —
    ///     the hash may only change how fast the answer is reached, never what it is.
    /// </remarks>
    [Fact]
    public void Detect_WhenEveryWindowCollidesInOneBucket_VerificationStillDecides() {
        var unrelated = (DuplicationInput[])[
            Production("/repo/Alpha.cs", Alpha(Block(400, seed: 1))),
            Production(
                "/repo/Beta.cs",
                Beta(Block(400, seed: 2))
            )
        ];

        Assert.Empty(CloneDetector.Detect(unrelated, MinTokens, null, TestContext.Current.CancellationToken).Groups);
        Assert.Empty(
            CloneDetector.Detect(unrelated, MinTokens, null, true, TestContext.Current.CancellationToken).Groups
        );

        var block = Block(250);
        var cloned = (DuplicationInput[])[
            Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block))
        ];

        Assert.Equal(
            Render(CloneDetector.Detect(cloned, MinTokens, null, TestContext.Current.CancellationToken)),
            Render(CloneDetector.Detect(cloned, MinTokens, null, true, TestContext.Current.CancellationToken))
        );
    }

    /// <summary>
    ///     ⚠ Out of the numerator <i>and</i> the denominator. A generated file that duplicates a production
    ///     file leaves the production file alone in its group, which is no group at all.
    /// </summary>
    [Fact]
    public void Detect_ExcludesGeneratedFilesFromBothHalvesOfThePercentage() {
        var block = Block(250);
        var production = Production("/repo/Alpha.cs", Alpha(block));
        var generated = new DuplicationInput("/repo/Beta.g.cs", Beta(block), true, false);

        var result = Detect([production, generated]);

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.DuplicatedLines);
        Assert.Equal(Lines(production.Text), result.TotalLines);
        Assert.Equal(0, result.TestTotalLines);
    }

    /// <summary>
    ///     docs/plan/09: "test files are counted separately, because test duplication is often deliberate
    ///     and gating it drives people to write worse tests".
    /// </summary>
    /// <remarks>
    ///     ⚠ Separately also means separately <i>matched</i>. The production and the test copy of the same
    ///     block are not one group, because a group that straddles the two would have to be counted in one
    ///     bucket or the other and either answer is wrong.
    /// </remarks>
    [Fact]
    public void Detect_CountsTestFilesSeparatelyFromProductionFiles() {
        var shared = Block(250, seed: 1);
        var testOnly = Block(250, seed: 2);

        var result = Detect(
            [
                Production("/repo/Core/Alpha.cs", Alpha(shared)), Production("/repo/Core/Beta.cs", Beta(shared)),
                Test(
                    "/repo/Core.Tests/AlphaTests.cs",
                    Alpha(testOnly)
                ), Test("/repo/Core.Tests/BetaTests.cs", Beta(testOnly))
            ]
        );

        var production = Assert.Single(result.Groups);
        Assert.All(
            production.Occurrences,
            static occurrence => Assert.DoesNotContain(".Tests", occurrence.Path, StringComparison.Ordinal)
        );

        var tests = Assert.Single(result.TestGroups);
        Assert.All(
            tests.Occurrences,
            static occurrence => Assert.Contains(".Tests", occurrence.Path, StringComparison.Ordinal)
        );

        Assert.True(result.DuplicatedLines > 0);
        Assert.True(result.TestDuplicatedLines > 0);
        Assert.True(result.Percentage > 0);
        Assert.True(result.TestPercentage > 0);

        // ⚠ The gate reads Percentage, and it must not have seen a single test line.
        var productionOnly = Detect(
            [
                Production("/repo/Core/Alpha.cs", Alpha(shared)), Production("/repo/Core/Beta.cs", Beta(shared))
            ]
        );

        Assert.Equal(productionOnly.TotalLines, result.TotalLines);
        Assert.Equal(productionOnly.DuplicatedLines, result.DuplicatedLines);
    }

    [Fact]
    public void Detect_WhenAProductionFileAndATestFileShareABlock_ReportsNeither() {
        var block = Block(250);

        var result = Detect(
            [Production("/repo/Alpha.cs", Alpha(block)), Test("/repo/Tests/BetaTests.cs", Beta(block))]
        );

        Assert.Empty(result.Groups);
        Assert.Empty(result.TestGroups);
    }

    /// <summary>
    ///     ⚠ The invariant that keeps the percentage a percentage. A line in three groups is one
    ///     duplicated line; counted once per group it would produce a duplication of 250 %.
    /// </summary>
    [Fact]
    public void Detect_WhenLinesBelongToSeveralGroups_CountsEachLineOnce() {
        var first = Block(250, seed: 3);
        var second = Block(250, seed: 4);
        var files = new List<DuplicationInput>();
        for (var i = 0; i < 4; i++) {
            files.Add(
                Production(
                    "/repo/File" + i.ToString(CultureInfo.InvariantCulture) + ".cs",
                    Alpha(first + "        int spacer" + i.ToString(CultureInfo.InvariantCulture) + " = 0;\n" + second)
                )
            );
        }

        var result = Detect(files);

        Assert.NotEmpty(result.Groups);
        Assert.True(
            result.DuplicatedLines <= result.TotalLines,
            $"{result.DuplicatedLines} duplicated of {result.TotalLines} total"
        );

        Assert.InRange(result.Percentage, 0d, 100d);
    }

    [Fact]
    public void Index_WhenTheFilesAreUnchanged_TheWarmRunMatchesTheColdRun() {
        using var scratch = new Scratch();
        var files = Corpus();

        var cold = CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(Path.Combine(scratch.Root, "clones.idx")));

        var warm = CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken);

        Assert.Equal(Render(cold), Render(warm));
        Assert.Equal(
            Render(CloneDetector.Detect(files, MinTokens, null, TestContext.Current.CancellationToken)),
            Render(warm)
        );
    }

    /// <summary>
    ///     ⚠ Three shapes of damage, one answer: a cold run. An index that half-loads is an index that
    ///     reports clones about code that is no longer in the file, and nothing downstream could tell.
    /// </summary>
    [Theory]
    [InlineData("garbage")]
    [InlineData("truncated")]
    [InlineData("flipped")]
    public void Index_WhenTheFileIsCorrupt_DegradesToAColdRun(string damage) {
        using var scratch = new Scratch();
        var files = Corpus();
        var path = Path.Combine(scratch.Root, "clones.idx");
        var expected = Render(CloneDetector.Detect(files, MinTokens, null, TestContext.Current.CancellationToken));

        switch (damage) {
            case "garbage":
                File.WriteAllText(path, "this is not a clone index, and it is not even close");
                break;

            default: {
                CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken);
                var bytes = File.ReadAllBytes(path);
                if (damage == "truncated") {
                    File.WriteAllBytes(path, bytes.AsSpan(0, bytes.Length / 2).ToArray());
                } else {
                    bytes[^7] ^= 0xFF;
                    File.WriteAllBytes(path, bytes);
                }

                break;
            }
        }

        Assert.Equal(
            expected,
            Render(CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken))
        );

        // …and the run that hit the damage left a good index behind, so the next one is warm.
        Assert.Equal(
            expected,
            Render(CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken))
        );
    }

    [Fact]
    public void Index_WhenAFileChanges_TheAnswerChangesWithIt() {
        using var scratch = new Scratch();
        var block = Block(250);
        var cloned = (DuplicationInput[])[
            Production("/repo/Alpha.cs", Alpha(block)), Production("/repo/Beta.cs", Beta(block))
        ];

        Assert.NotEmpty(
            CloneDetector.Detect(cloned, MinTokens, scratch.Root, TestContext.Current.CancellationToken).Groups
        );

        var edited = (DuplicationInput[])[
            cloned[0], Production("/repo/Beta.cs", Beta(Block(250, seed: 9)))
        ];

        Assert.Empty(
            CloneDetector.Detect(edited, MinTokens, scratch.Root, TestContext.Current.CancellationToken).Groups
        );
    }

    [Fact]
    public void ToFindings_ReportsOneWarningAtTheFirstOccurrence_NamingTheOthers() {
        var block = Block(250);
        var result = Detect(
            [
                Production("/repo/Core/Alpha.cs", Alpha(block)), Production("/repo/Editor/Beta.cs", Beta(block))
            ]
        );

        var finding = Assert.Single(CloneDetector.ToFindings(result, "/repo"));
        var group = result.Groups[0];

        Assert.Equal(RuleIds.DuplicatedBlock, finding.RuleId);
        Assert.Equal(SkalaSeverity.Warning, finding.Severity);
        Assert.Equal(group.Occurrences[0].Path, finding.Path);
        Assert.Equal(group.Occurrences[0].StartLine, finding.Line);
        Assert.Equal(group.Occurrences[0].EndLine, finding.EndLine);
        Assert.Equal(group.Occurrences[0].Start, finding.Start);
        Assert.Equal(group.Occurrences[0].Length, finding.Length);

        // The other occurrence is in the message, relative to the root, because Finding has no
        // related-locations field to put it in.
        Assert.Contains("250 tokens", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Editor/Beta.cs:", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("/repo/Editor", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Test duplication is measured and reported, never gated. See <c>CloneDetector.ToFindings</c>.
    /// </summary>
    [Fact]
    public void ToFindings_DoesNotReportTestGroups() {
        var block = Block(250);
        var result = Detect(
            [
                Test("/repo/Core.Tests/AlphaTests.cs", Alpha(block)), Test("/repo/Core.Tests/BetaTests.cs", Beta(block))
            ]
        );

        Assert.Single(result.TestGroups);
        Assert.Empty(CloneDetector.ToFindings(result, "/repo"));
    }

    [Fact]
    public void Detect_IsDeterministic_WhateverOrderTheFilesArriveIn() {
        var block = Block(250);
        var files = (DuplicationInput[])[
            Production("/repo/Gamma.cs", Gamma(block)), Production("/repo/Alpha.cs", Alpha(block)),
            Production(
                "/repo/Beta.cs",
                Beta(block)
            )
        ];

        var forwards = Render(Detect(files));
        Array.Reverse(files);

        Assert.Equal(forwards, Render(Detect(files)));
    }

    /// <summary>
    ///     ⚠ The header is not duplication — issue #323.
    /// </summary>
    /// <remarks>
    ///     Identifiers normalise to <c>IdentifierToken</c>, so two files' <c>using</c> blocks match on the
    ///     number of dotted segments in the same order and on nothing else. <see cref="Header" /> is 110
    ///     tokens, comfortably over the 100-token window, so before the skip these two unrelated files were
    ///     a clone of each other before either had done anything.
    ///     <para>
    ///         ⚠ If this goes green with the skip reverted, the skip is not what is being measured.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Detect_WhenTwoFilesShareOnlyTheirHeader_ReportsNothing() {
        var result = Detect(
            [
                Production("/repo/Alpha.cs", Header("Alpha") + Alpha(Block(150, seed: 1))),
                Production("/repo/Beta.cs", Header("Beta") + Beta(Block(150, seed: 2)))
            ]
        );

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.DuplicatedLines);
    }

    /// <summary>
    ///     ⚠ The other half, and the one that says the skip removed noise rather than signal: the same two
    ///     headers, and a body they genuinely share, is still exactly one group of exactly the body.
    /// </summary>
    [Fact]
    public void Detect_WhenTheBodiesDuplicate_TheDifferentHeadersDoNotHideIt() {
        var block = Block(150);

        var result = Detect(
            [
                Production("/repo/Alpha.cs", Header("Alpha") + Alpha(block)),
                Production("/repo/Beta.cs", Header("Beta") + Beta(block))
            ]
        );

        var group = Assert.Single(result.Groups);
        Assert.Equal(150, group.TokenLength);
        Assert.Equal(2, group.Occurrences.Length);
    }

    /// <summary>
    ///     ⚠ <b>The boundary is the node type, never the word.</b>
    /// </summary>
    /// <remarks>
    ///     A <c>using</c> directive is a <c>UsingDirectiveSyntax</c>; <c>using var x = …</c> is a
    ///     <c>LocalDeclarationStatementSyntax</c> and <c>using (…) { }</c> is a <c>UsingStatementSyntax</c>.
    ///     The last two are resource management — real code, and a place duplication genuinely hides. A skip
    ///     that matched on the token <c>using</c> would blind the detector to all of it, and nothing else
    ///     here would notice.
    /// </remarks>
    [Fact]
    public void Lex_SkipsUsingDirectives_AndKeepsUsingStatements() {
        const string source = """
                              using System;

                              namespace Sample;

                              class Probe {
                                  void Run() {
                                      using var first = Open();
                                      using (var second = Open()) {
                                      }
                                  }
                              }
                              """;

        // `using System;` is 3 tokens and `namespace Sample;` is 3; every other token survives, the
        // two body `using`s included.
        Assert.Equal(TokenCount(source) - 6, TokenStream.Lex(source).Count);
    }

    /// <summary>
    ///     ⚠ The two namespace forms are skipped to different points, deliberately.
    /// </summary>
    /// <remarks>
    ///     A file-scoped declaration goes through its <c>;</c>, because <c>namespace ID . ID ;</c> is the
    ///     same artefact as a <c>using</c>. A block declaration stops at the end of its <b>name</b>: the
    ///     <c>{</c>, the members and the closing <c>}</c> are still tokenised, so the brace nesting a
    ///     block-scoped file has is still what it is compared on.
    /// </remarks>
    [Fact]
    public void Lex_SkipsTheNamespaceHeader_ButNotABlockNamespaceBrace() {
        const string blockScoped = "namespace Sample.Inner { class Probe { } }";
        const string fileScoped = "namespace Sample.Inner;\nclass Probe { }";

        // `namespace Sample . Inner` is 4 tokens; `{ class Probe { } }` is not touched.
        Assert.Equal(TokenCount(blockScoped) - 4, TokenStream.Lex(blockScoped).Count);

        // …and the file-scoped form takes its `;` too.
        Assert.Equal(TokenCount(fileScoped) - 5, TokenStream.Lex(fileScoped).Count);
    }

    /// <summary>
    ///     ⚠ Out of the numerator <i>and</i> the denominator, exactly as a generated file is.
    /// </summary>
    /// <remarks>
    ///     A line that can never be matched must not dilute the ratio either. Removing the header from one
    ///     half only would move <c>metrics.duplication</c> for a second reason — downwards for every import
    ///     anyone adds — and make the change unattributable.
    ///     <para>
    ///         ⚠ A line holding no tokens at all is neither, so the blank line between the directives and
    ///         the namespace stays counted. <see cref="Header" /> has none, so the arithmetic here is exact.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Detect_TakesTheHeaderOutOfTheDenominatorToo() {
        var text = Header("Alpha") + Alpha(Block(150));

        var result = Detect([Production("/repo/Alpha.cs", text)]);

        Assert.Equal(Lines(text) - HeaderLines, result.TotalLines);
    }

    /// <summary>
    ///     ⚠ The tokeniser's identity is in the index's stamp — issue #322.
    /// </summary>
    /// <remarks>
    ///     <c>clones.idx</c> is keyed on <c>(path, content hash)</c> and stamped with the format and tool
    ///     versions, none of which move when <c>TokenStream.Lex</c> is edited. A change to duplication
    ///     detection therefore used to measure itself against the <i>previous</i> tokeniser's streams:
    ///     12.89 % warm against 6.9 % cold, one binary, one tree, identical finding sets, no warning.
    ///     <para>
    ///         ⚠ This asserts the canary is broad enough to notice, which is the half that can rot. A
    ///         fingerprint over a canary that exercises nothing is a fingerprint that never moves, and it
    ///         would look exactly like this one.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Fingerprint_IsDerivedFromACanaryThatExercisesTheLexer() {
        var lexed = TokenStream.Lex(TokenStream.Canary);

        Assert.True(lexed.HeaderLines > 0, "the canary must exercise the header skip");
        Assert.True(lexed.Count > 0 && lexed.Count < TokenCount(TokenStream.Canary), "the canary must lose tokens");
        Assert.Equal(32, TokenStream.Fingerprint.Length);
        Assert.Equal(TokenStream.Fingerprint, TokenStream.Fingerprint);
    }

    /// <summary>
    ///     ⚠ And a stamp that does not match this build's is a cold run, not a stale answer.
    /// </summary>
    /// <remarks>
    ///     Patching the stamp in the header is what a tokeniser change does to an index written by the
    ///     previous build. The header is not covered by the payload checksum, so this is the stamp check
    ///     being exercised and not the corruption path beside it.
    /// </remarks>
    [Fact]
    public void Index_WhenTheStampIsNotThisBuilds_DegradesToAColdRun() {
        using var scratch = new Scratch();
        var files = Corpus();
        var path = Path.Combine(scratch.Root, "clones.idx");
        var expected = Render(CloneDetector.Detect(files, MinTokens, null, TestContext.Current.CancellationToken));

        CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken);
        var bytes = File.ReadAllBytes(path);

        // The stamp is a length-prefixed string at offset 12; its last byte is inside the fingerprint.
        var stampLength = BitConverter.ToInt32(bytes, 8);
        Assert.InRange(stampLength, 33, 128);
        bytes[11 + stampLength] ^= 0x01;
        File.WriteAllBytes(path, bytes);

        Assert.Equal(
            expected,
            Render(CloneDetector.Detect(files, MinTokens, scratch.Root, TestContext.Current.CancellationToken))
        );
    }

    [Fact]
    public void Detect_WhenMinTokensIsBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(static () => CloneDetector.Detect(
                [],
                0,
                null,
                TestContext.Current.CancellationToken
            )
        );

    static DuplicationResult Detect(IReadOnlyList<DuplicationInput> files) =>
        CloneDetector.Detect(files, MinTokens, null, TestContext.Current.CancellationToken);

    static DuplicationInput Production(string path, string text) => new(path, text, false, false);

    static DuplicationInput Test(string path, string text) => new(path, text, false, true);

    static DuplicationInput[] Corpus() {
        var shared = Block(250, seed: 5);
        return [
            Production("/repo/Alpha.cs", Alpha(shared)), Production("/repo/Beta.cs", Beta(shared)),
            Production(
                "/repo/Gamma.cs",
                Gamma(Block(250, seed: 6))
            ), Test("/repo/Core.Tests/AlphaTests.cs", Alpha(Block(180, seed: 7))),
            Test(
                "/repo/Core.Tests/BetaTests.cs",
                Beta(Block(180, seed: 7))
            )
        ];
    }

    /// <summary>Everything an assertion could care about, as one comparable string.</summary>
    static string Render(DuplicationResult result) {
        var builder = new StringBuilder();
        Render(builder, "production", result.Groups);
        Render(builder, "test", result.TestGroups);
        return builder.Append(CultureInfo.InvariantCulture, $"{result.DuplicatedLines}/{result.TotalLines} ")
            .Append(CultureInfo.InvariantCulture, $"{result.TestDuplicatedLines}/{result.TestTotalLines}")
            .ToString();
    }

    static void Render(StringBuilder builder, string label, IEnumerable<CloneGroup> groups) {
        foreach (var group in groups) {
            builder.Append(CultureInfo.InvariantCulture, $"{label} {group.TokenLength}:");
            foreach (var occurrence in group.Occurrences) {
                builder.Append(CultureInfo.InvariantCulture, $" {occurrence.Path}@{occurrence.Start}")
                    .Append(CultureInfo.InvariantCulture, $"+{occurrence.Length}")
                    .Append(CultureInfo.InvariantCulture, $"[{occurrence.StartLine}-{occurrence.EndLine}]");
            }

            builder.Append('\n');
        }
    }

    static int Lines(string text) => Microsoft.CodeAnalysis.Text.SourceText.From(text).Lines.Count;

    /// <summary>
    ///     A file header of 110 tokens and <see cref="HeaderLines" /> lines, containing no logic at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="seed" /> changes every name and <b>nothing else</b>, which is the whole point:
    ///     the normalisation maps identifiers to one class, so two headers built from different seeds lex to
    ///     byte-identical token streams. That is the artefact — files matching on the number of dotted
    ///     segments in the same order — and it is why 289 analyzer files were clones of each other.
    /// </remarks>
    static string Header(string seed) {
        var builder = new StringBuilder();
        for (var i = 0; i < HeaderDirectives; i++) {
            builder.Append(CultureInfo.InvariantCulture, $"using {seed}{i}.Second{i}.Third{i};\n");
        }

        return builder.Append(CultureInfo.InvariantCulture, $"namespace {seed}.Root;\n").ToString();
    }

    static int TokenCount(string text) {
        var count = 0;
        foreach (var token in SyntaxFactory.ParseTokens(text)) {
            if (token.RawKind != (int)SyntaxKind.EndOfFileToken && token.Span.Length > 0) {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     A statement block of exactly <paramref name="tokens" /> tokens.
    /// </summary>
    /// <remarks>
    ///     ⚠ The shape sequence is pseudo-random rather than a cycle, and that is load-bearing. A block
    ///     built by cycling through the shapes matches <i>itself</i> shifted by one cycle — a tandem
    ///     repeat — and every assertion here about a group's length and occurrence count would then be
    ///     measuring the detector's tandem-repeat handling instead of what it says it measures.
    /// </remarks>
    static string Block(
        int tokens,
        string prefix = "item",
        string call = "Fetch",
        string type = "Node",
        uint seed = 0
    ) {
        Assert.True(tokens >= 3, "a block of fewer than three tokens is not a statement");

        var builder = new StringBuilder();
        var state = 2026_08_27u + seed * 7919u;
        var remaining = tokens;
        var index = 0;

        while (remaining > 0) {
            state = unchecked(state * 1664525u + 1013904223u);
            var start = (int)(state >> 13) % Shapes.Length;
            for (var offset = 0; offset < Shapes.Length; offset++) {
                var shape = Shapes[(start + offset) % Shapes.Length];
                if (shape.Tokens > remaining || remaining - shape.Tokens is not 0 and < 3) {
                    continue;
                }

                builder.Append("        ")
                    .AppendFormat(CultureInfo.InvariantCulture, shape.Format, prefix, index, call, type)
                    .Append('\n');
                remaining -= shape.Tokens;
                index++;
                break;
            }
        }

        return builder.ToString();
    }

    // ⚠ The three wrappers end their prelude with a different token kind (`;`, `}`, `}`) and open
    // their epilogue with a different keyword (`return`, `throw`, `checked`), so a group's greedy
    // extension stops exactly at the block. Alpha is in every multi-file fixture, because it is the
    // one whose `;` stops a left extension that Beta and Gamma would agree on.
    /// <summary>
    ///     A collection expression of <paramref name="count" /> identical elements: four tokens each and a
    ///     stride of five.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every element is spelled with a different name and they are all one token sequence, which is
    ///     the whole artefact — <c>new Kind0(),</c> and <c>new Kind1(),</c> are indistinguishable once
    ///     identifiers normalise, so the list is a periodic token stream that matches itself shifted by any
    ///     multiple of five.
    /// </remarks>
    static string UniformList(int count) {
        var builder = new StringBuilder("class Table {\n    static readonly object[] All = [\n");
        for (var i = 0; i < count; i++) {
            builder.Append(CultureInfo.InvariantCulture, $"        new Kind{i}(),\n");
        }

        return builder.Append("    ];\n}\n").ToString();
    }

    /// <summary>
    ///     <paramref name="count" /> members of one class, each holding the same block.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sibling members that lex alike, exactly like <see cref="UniformList" /> — and unlike it, real
    ///     duplication, because each element is longer than the detection window. The two fixtures differ
    ///     in nothing but the size of a row, which is the line the detector has to draw.
    /// </remarks>
    static string Members(string block, int count) {
        var builder = new StringBuilder("class Holder {\n");
        for (var i = 0; i < count; i++) {
            builder.Append(CultureInfo.InvariantCulture, $"    void Run{i}() {{\n{block}    }}\n");
        }

        return builder.Append("}\n").ToString();
    }

    static string Alpha(string block) =>
        "class Alpha {\n    void Run() {\n        double seed = 3.5;\n" + block + "        return;\n    }\n}\n";

    static string Beta(string block) =>
        "struct Beta {\n    public void Go() {\n        if (Ready) { }\n"
        + block
        + "        throw new System.Exception();\n    }\n}\n";

    static string Gamma(string block) =>
        "record Gamma {\n    internal static long Step(byte flag) {\n        switch (flag) { default: break; }\n"
        + block
        + "        checked { }\n        return 0;\n    }\n}\n";
}
