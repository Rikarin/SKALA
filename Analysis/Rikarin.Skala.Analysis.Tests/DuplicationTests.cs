using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Duplication;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
/// docs/plan/09 § "Duplication" — token-level type-2 clone detection, and <c>SK7020</c>.
/// </summary>
/// <remarks>
/// ⚠ Every assertion about a group's <c>TokenLength</c> rests on the fixture below producing blocks
/// with exactly the token count they are asked for, which is why
/// <see cref="Block_HasExactlyTheTokensItClaims"/> exists and runs first in the file.
/// </remarks>
public sealed class DuplicationTests {
    const int MinTokens = 100;

    /// <summary>
    /// Statement shapes and the exact number of tokens each lexes to, trivia dropped.
    /// </summary>
    /// <remarks>
    /// ⚠ Every count from 3 to 11 is present, so <see cref="Block"/> can always land on an exact
    /// total: from any remainder of 3 or more there is a shape that consumes it or leaves 3 or more.
    /// </remarks>
    static readonly (int Tokens, string Format)[] Shapes = [
        (3, "{0}{1}++;"), (4, "{0}{1} = {0}{1};"), (5, "{0}{1} = -1;"), (6, "{0}{1} = {0}{1} + 1;"),
        (7, "{0}{1} = {2}({0}{1});"),
        (8, "{0}{1} = {0}{1} * {0}{1} - 3;"), (9, "if ({0}{1} > 1) {0}{1}++;"),
        (10, "{0}{1} = new {3}({0}{1}, \"s\");"),
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
    /// ⚠ The type-2 property, and the reason the rule exists: an agent's copy-paste is a copy with the
    /// variables renamed. If this test goes red the rule has silently become type-1 detection, which
    /// finds almost nothing in real code.
    /// </summary>
    [Fact]
    public void Detect_WhenEveryIdentifierIsRenamed_StillReportsOneGroup() {
        var original = Block(120, prefix: "value", call: "Compute", type: "Holder");
        var renamed = Block(120, prefix: "otherName", call: "Evaluate", type: "Widget");
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
    /// docs/plan/09 step 4, and <c>SK7020</c>'s rationale: "reporting it at every occurrence would turn
    /// one problem into n findings and make the count meaningless".
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
    /// ⚠ A 250-token match contains 151 overlapping 100-token windows. Every one of them is a verified
    /// clone class, and all 151 have to collapse into one maximal group.
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
    /// <c>SK7020</c>'s <c>falsePositives</c>: "the match is verified exactly rather than trusted from
    /// the rolling hash, so a hash collision cannot produce a finding".
    /// </summary>
    /// <remarks>
    /// ⚠ Collapsing every window into one bucket is a worse collision than could ever occur by
    /// accident. Unrelated files must still report nothing, and a real clone must come out identical —
    /// the hash may only change how fast the answer is reached, never what it is.
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
    /// ⚠ Out of the numerator <i>and</i> the denominator. A generated file that duplicates a production
    /// file leaves the production file alone in its group, which is no group at all.
    /// </summary>
    [Fact]
    public void Detect_ExcludesGeneratedFilesFromBothHalvesOfThePercentage() {
        var block = Block(250);
        var production = Production("/repo/Alpha.cs", Alpha(block));
        var generated = new DuplicationInput("/repo/Beta.g.cs", Beta(block), IsGenerated: true, IsTest: false);

        var result = Detect([production, generated]);

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.DuplicatedLines);
        Assert.Equal(Lines(production.Text), result.TotalLines);
        Assert.Equal(0, result.TestTotalLines);
    }

    /// <summary>
    /// docs/plan/09: "test files are counted separately, because test duplication is often deliberate
    /// and gating it drives people to write worse tests".
    /// </summary>
    /// <remarks>
    /// ⚠ Separately also means separately <i>matched</i>. The production and the test copy of the same
    /// block are not one group, because a group that straddles the two would have to be counted in one
    /// bucket or the other and either answer is wrong.
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
            occurrence => Assert.DoesNotContain(".Tests", occurrence.Path, StringComparison.Ordinal)
        );

        var tests = Assert.Single(result.TestGroups);
        Assert.All(
            tests.Occurrences,
            occurrence => Assert.Contains(".Tests", occurrence.Path, StringComparison.Ordinal)
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
    /// ⚠ The invariant that keeps the percentage a percentage. A line in three groups is one
    /// duplicated line; counted once per group it would produce a duplication of 250 %.
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
    /// ⚠ Three shapes of damage, one answer: a cold run. An index that half-loads is an index that
    /// reports clones about code that is no longer in the file, and nothing downstream could tell.
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

    /// <summary>⚠ Test duplication is measured and reported, never gated. See <c>CloneDetector.ToFindings</c>.</summary>
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

    [Fact]
    public void Detect_WhenMinTokensIsBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CloneDetector.Detect(
                [],
                0,
                null,
                TestContext.Current.CancellationToken
            )
        );

    static DuplicationResult Detect(IReadOnlyList<DuplicationInput> files) =>
        CloneDetector.Detect(files, MinTokens, null, TestContext.Current.CancellationToken);

    static DuplicationInput Production(string path, string text) => new(path, text, IsGenerated: false, IsTest: false);

    static DuplicationInput Test(string path, string text) => new(path, text, IsGenerated: false, IsTest: true);

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
    /// A statement block of exactly <paramref name="tokens"/> tokens.
    /// </summary>
    /// <remarks>
    /// ⚠ The shape sequence is pseudo-random rather than a cycle, and that is load-bearing. A block
    /// built by cycling through the shapes matches <i>itself</i> shifted by one cycle — a tandem
    /// repeat — and every assertion here about a group's length and occurrence count would then be
    /// measuring the detector's tandem-repeat handling instead of what it says it measures.
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
