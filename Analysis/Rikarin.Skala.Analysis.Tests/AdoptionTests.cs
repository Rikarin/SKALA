using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     The defects the first real adoption found, each one a run that came back green — or red — for
///     the wrong reason.
/// </summary>
/// <remarks>
///     ⚠ Every one of these survived four milestones of a green suite, and the absence of the test is
///     the actual defect: each was reachable only by running the documented step on a repository that
///     already had history, and nothing in the suite did that.
/// </remarks>
public sealed class AdoptionTests {
    /// <summary>
    ///     A line with no break point anywhere in it. ⚠ One identifier and one string, both atomic —
    ///     this is the shape `SK0002` exists for, and the shape `skala format` cannot do anything about.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the formatter's <em>own output</em> for that declaration — it breaks after the
    ///     <c>=</c> and then has nowhere left to go, because a string literal is atomic. So the file is
    ///     a fixed point: <c>format --check</c> reports "0 files would be reformatted" and `SK0002`
    ///     fires anyway. If it were not already formatted the test would prove nothing, because
    ///     `SK0001` would be present and the gate would be right to fail.
    /// </remarks>
    const string UnbreakableLongLine = """
                                       namespace Scratch;

                                       public static class Wide {
                                           public const string Reference =
                                               "https://example.invalid/a-very-long-path-segment-that-simply-does-not-contain-anything-the-formatter-could-break-on-at-all";
                                       }

                                       """;

    /// <summary>A gate whose only condition is the one under test.</summary>
    const string FormattingGate = """
                                  { "gates": { "formatting": { "formatting": "clean" } } }
                                  """;

    static CheckRequest Request(Scratch scratch) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            Output = string.Empty,
            NoCache = true
        };

    /// <summary>
    ///     ⚠
    ///     <b>
    ///         `formatting: clean` counted findings the formatter refuses to fix, and was therefore
    ///         unsatisfiable.
    ///     </b> Measured on Vixen's <c>Core/Vixen.Water</c> after a full
    ///     <c>
    /// skala
    ///  format
    ///     </c>: <c>format --check</c> reported "0 files would be reformatted" and the <c>ci</c>
    ///     gate still failed with "formatting is not clean; run `skala format`" on 23 <c>SK0002</c>.
    ///     Running the formatter changed nothing, and the bit was computed before scoping so a baseline
    ///     could not absorb them either. Any repository holding one unbreakable long line was locked
    ///     out of the gate.
    /// </summary>
    [Fact]
    public void FormattingClean_IsSatisfiedByAFileTheFormatterWillNotTouch() {
        using var scratch = new Scratch();
        scratch.Write("Wide.cs", UnbreakableLongLine);

        scratch.Write("skala.jsonc", FormattingGate);

        var (_, report) = CheckCommand.Run(
            Request(scratch) with { Gate = "formatting" },
            TestContext.Current.CancellationToken
        );

        // The premise: SK0002 fires, and SK0001 does not — the file is already formatted.
        Assert.Contains(report.Findings, static finding => finding.RuleId == RuleIds.LineTooLongUnbreakable);
        Assert.DoesNotContain(report.Findings, static finding => finding.RuleId == RuleIds.FileIsNotFormatted);

        Assert.True(
            report.Gate!.Passed,
            "`formatting: clean` means `format --check` produces no edits. SK0002 is not an edit — "
            + "the formatter reports it precisely because there is nothing it can safely change:\n  "
            + string.Join("\n  ", report.Gate.Failures)
        );
    }

    /// <summary>And a file that really does need formatting still fails it.</summary>
    [Fact]
    public void FormattingClean_StillFailsOnAFileThatNeedsFormatting() {
        using var scratch = new Scratch();
        scratch.Write("Ugly.cs", "public sealed class Ugly{public int    Value;}");

        scratch.Write("skala.jsonc", FormattingGate);

        var (_, report) = CheckCommand.Run(
            Request(scratch) with { Gate = "formatting" },
            TestContext.Current.CancellationToken
        );

        Assert.Contains(report.Findings, static finding => finding.RuleId == RuleIds.FileIsNotFormatted);
        Assert.False(report.Gate!.Passed);
    }

    /// <summary>
    ///     ⚠ <b>`skala verify` had neither `--baseline` nor `--since`.</b> On the adopted tree it
    ///     reported 778 findings needing a decision, every run, for ever — the one command doc 10 tells
    ///     an agent to run was the one command that could not be told what had already been accepted.
    /// </summary>
    [Fact]
    public void Verify_WithABaseline_HasNothingToDoOnceEverythingIsAccepted() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsWork);

        var request = new VerifyRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], NoCache = true };

        Assert.NotEqual(ExitCodes.Ok, VerifyCommand.Run(request, TestContext.Current.CancellationToken).ExitCode);

        var baselinePath = Path.Combine(scratch.Root, ".skala", "baseline.sarif");
        BaselineCommand.Run(
            BaselineCommand.Verb.Create,
            Request(scratch) with { BaselinePath = baselinePath, IncludeArrangement = true },
            true,
            TestContext.Current.CancellationToken
        );

        var scoped = VerifyCommand.Run(
            request with { BaselinePath = baselinePath },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, scoped.ExitCode);

        // ⚠ And the report agrees with the exit code. Reading `Reportable` in the renderer while
        // the exit code read `New` produced "OK" beside a list of findings needing a decision,
        // which is worse than either alone.
        Assert.DoesNotContain("needs a decision", scoped.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("need a decision", scoped.Output, StringComparison.Ordinal);
    }

    /// <summary>An unscoped `verify` is unchanged: everything is still to do.</summary>
    [Fact]
    public void Verify_WithNoScoping_IsUnchanged() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsWork);

        var result = VerifyCommand.Run(
            new VerifyRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], NoCache = true },
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(ExitCodes.Ok, result.ExitCode);
    }

    /// <summary>
    ///     ⚠
    ///     <b>
    ///         `skala explain` is documented as taking `&lt;ruleId | optionKey&gt;` and rejected every
    ///         option key tried
    ///     </b> — <c>insert_final_newline</c>,
    ///     <c>dotnet_sort_system_directives_first</c> — with "is not a Skala rule". The two halves of
    ///     what Skala reads are rules and options, and only one of them could be asked about.
    /// </summary>
    [Theory]
    [InlineData("insert_final_newline")]
    [InlineData("dotnet_sort_system_directives_first")]
    [InlineData("resharper_csharp_max_line_length")]
    public void Explain_AnswersAnOptionKey(string key) {
        var result = ExplainCommand.Run(key);

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.DoesNotContain("is not a Skala rule", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_StillAnswersARuleId() {
        var result = ExplainCommand.Run("SK3002");

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.Contains("SK3002", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A rule-shaped token gets a rule-shaped answer even when nothing is near it. Telling
    ///     somebody who typed <c>SK9999</c> that it is "neither a rule nor an option" sends them
    ///     looking for the option.
    /// </summary>
    [Fact]
    public void Explain_OnAnUnknownRuleShapedToken_TalksAboutRules() {
        var result = ExplainCommand.Run("SK9999");

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains("is not a Skala rule", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_OnAMisspeltOptionKey_Suggests() {
        var result = ExplainCommand.Run("insert_final_newlines");

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains("insert_final_newline", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <b>The file walkers never excluded `.skala/`</b>, which holds the crash reproductions
    ///     Skala writes when the formatter trips — <c>crash/&lt;hash&gt;/input.cs</c> and
    ///     <c>output.cs</c>. So the first SK9099 on a repository made every later run analyse Skala's
    ///     own evidence as if it were the user's code, and reformatting a crash reproduction destroys
    ///     the thing it exists to preserve. Measured on Vixen: the selected-file count moved from 4 717
    ///     to 4 727 after ten reproductions had been written.
    /// </summary>
    [Fact]
    public void CrashReproductions_AreNotAnalysed() {
        using var scratch = new Scratch();
        scratch.Write("Real.cs", "namespace Scratch;\n\npublic sealed class Real;\n");
        scratch.Write(Path.Combine(".skala", "crash", "abc123", "input.cs"), "public sealed class Crash{int    x;}");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(
            loaded.Units.SelectMany(static unit => unit.Compilation.SyntaxTrees).Select(static tree => tree.FilePath),
            static path => path.Contains("crash", StringComparison.Ordinal)
        );
    }

    /// <summary>
    ///     ⚠ <b>A binlog from an incremental build is partial, and every command reported success.</b>
    ///     Age cannot see it: the binlog is seconds old, it simply holds only the projects MSBuild
    ///     rebuilt. The numbers below are the measured ones, from the Vixen scratch copy — a complete
    ///     build and an incremental one, four minutes apart, against the same 4 717 files.
    /// </summary>
    [Theory]
    // An incremental build: 52 of 4 717 covered. This is the defect, and the flag must refuse it.
    [InlineData(4717, 4665, true, 1, true)]
    // A complete build: 4 642 of 4 717. The missing 75 are one project the solution does not build,
    // and refusing here would make the flag unsatisfiable on the repository it was written for.
    [InlineData(4717, 75, true, 98, false)]
    // A cold `--no-incremental` build covers everything.
    [InlineData(4717, 0, true, 100, false)]
    // Without the flag, incompleteness is reported and never refused.
    [InlineData(4717, 4665, false, 1, false)]
    public void AnIncompleteBinlog_IsRefusedOnlyWhenTheCallerAskedForOne(
        int selected,
        int missing,
        bool requireFresh,
        int expectedPercent,
        bool expectedRefusal
    ) {
        Assert.Equal(expectedPercent, BinlogLoader.CoveragePercent(selected, missing));
        Assert.Equal(
            expectedRefusal ? SkalaSeverity.Error : SkalaSeverity.Warning,
            BinlogLoader.CoverageSeverity(selected, missing, requireFresh)
        );
    }

    const string NeedsWork = """
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
}
