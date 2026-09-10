using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #359: the writing verbs of <c>skala baseline</c> refuse a run that could not read every file it
///     was asked about. A baseline is the denominator every later run is compared against, and one
///     recorded from a tree with an unreadable file is short by exactly that file's findings — silently,
///     permanently, and reported as <em>new</em> on the day the file becomes readable again.
/// </summary>
/// <remarks>
///     <para>
///         Measured through the real binary before the fix, over a readable file beside a mode-000
///         one: <c>baseline create --apply --load loose</c> printed <c>1 finding(s) firing now</c>,
///         <c>written.</c>, and exited <b>0</b> with no <c>SK9015</c> anywhere in its output, and
///         <c>update --apply</c> rewrote the file the same way. <c>check</c> over the same tree
///         printed the <c>SK9015</c> and exited 5. After the fix the unlocked tree writes
///         <c>2 finding(s)</c> — the second is the one the accepted baseline never held.
///     </para>
///     <para>
///         ⚠ The guard #309 wrote keyed on <c>RunReport.Partial</c>, which only a cancelled analyzer
///         sets, and the stop above it keyed on <c>LoadFailure</c>, which an unreadable file is
///         deliberately not (#357). The comment between them already described this harm; the
///         condition under it was narrower than the sentence.
///     </para>
///     <para>
///         ⚠ <b>Sabotage:</b> drop <c>unreadable</c> from <c>BaselineCommand</c>'s <c>unreliable</c>
///         expression and the two locked fixtures go red on the exit code and on the written file. The
///         unlocked controls stay green, as they should — they exist so that the locked halves cannot
///         pass because the tree itself was refused.
///     </para>
///     <para>
///         ⚠ The mode-000 fixture decides by attempting the read (<see cref="Scratch.WriteUnreadable" />):
///         root opens a mode-000 file, and every assertion here would then hold for the wrong reason.
///         The bits are restored in teardown either way.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class BaselineWriteTests {
    const string Skip = "needs a POSIX mode bit this process is subject to; root and Windows are exempt.";

    const string Readable = """
                            namespace Scratch;

                            public sealed class Widget {
                                public int Value { get; set; }
                            }
                            """;

    const string Locked = """
                          namespace Scratch;

                          public sealed class Gadget {
                              public int Value { get; set; }
                          }
                          """;

    static CheckRequest Request(Scratch scratch) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            NoCache = true
        };

    static (CommandResult Result, RunReport Report) Run(BaselineCommand.Verb verb, Scratch scratch, bool apply) =>
        BaselineCommand.Run(verb, Request(scratch), apply, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(BaselineCommand.Verb.Create)]
    [InlineData(BaselineCommand.Verb.Update)]
    public void WritingVerbs_OverATreeWithAnUnreadableFile_RefuseAndWriteNothing(BaselineCommand.Verb verb) {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Readable);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var baseline = Baseline.DefaultPath(scratch.Root);

        var (result, report) = Run(verb, scratch, true);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Contains("refusing to write", result.Output, StringComparison.Ordinal);
        Assert.Contains("SK9015", result.Output, StringComparison.Ordinal);
        Assert.Contains(locked, result.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was written", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("  written.", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", result.Output, StringComparison.Ordinal);

        // ⚠ The refusal is not a `LoadFailure` and must not be reported as one: the report exists,
        // the readable neighbour was analysed, and the unreadable file is in the count (#356).
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.Equal(2, report.FileCount);

        Assert.False(File.Exists(baseline), "the baseline must not be written from a tree the run could not read");
    }

    /// <summary>
    ///     ⚠ The control that keeps the refusal above from passing for the wrong reason: the same tree
    ///     with the bits restored writes and exits 0, so the refusal is the unreadable file's and not the
    ///     tree's. Run as one fixture rather than two so the "same command" claim is literal.
    /// </summary>
    [Theory]
    [InlineData(BaselineCommand.Verb.Create)]
    [InlineData(BaselineCommand.Verb.Update)]
    public void WritingVerbs_AfterTheFileIsMadeReadable_WriteAndExitClean(BaselineCommand.Verb verb) {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Readable);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is not { } locked) {
            Assert.Skip(Skip);
            return;
        }

        var baseline = Baseline.DefaultPath(scratch.Root);

        var refused = Run(verb, scratch, true);
        Assert.Equal(ExitCodes.InternalError, refused.Result.ExitCode);
        Assert.False(File.Exists(baseline));

        Scratch.Unlock(locked);

        var (result, report) = Run(verb, scratch, true);

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.Contains("  written.", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("SK9015", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(report.Diagnostics, static d => d.Id == "SK9015");
        Assert.True(File.Exists(baseline));

        // ⚠ The written baseline holds the formerly locked file's findings — the ones the pre-fix
        // baseline was missing. Both files are identical apart from the type name, so each fires
        // the same rules and the locked one accounts for half of what was accepted.
        var accepted = Baseline.Read(baseline);
        Assert.Contains(accepted.Entries, static entry => entry.Path.EndsWith("Locked.cs", StringComparison.Ordinal));
        Assert.Contains(accepted.Entries, static entry => entry.Path.EndsWith("Widget.cs", StringComparison.Ordinal));
    }

    /// <summary>Reading is not recording: <c>show</c> stays exempt, as the #309 remark says.</summary>
    [Fact]
    public void Show_OverATreeWithAnUnreadableFile_StillAnswers() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Readable);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is null) {
            Assert.Skip(Skip);
            return;
        }

        var (result, _) = Run(BaselineCommand.Verb.Show, scratch, false);

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.Contains("no baseline yet", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("refusing to write", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Without <c>--apply</c> nothing is written either way, but the refusal still has to be said:
    ///     a dry run that prints <c>1 finding(s) firing now</c> over a two-file tree is the same false
    ///     denominator on paper.
    /// </summary>
    [Fact]
    public void Create_WithoutApply_OverATreeWithAnUnreadableFile_StillRefuses() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Readable);
        if (scratch.WriteUnreadable("Locked.cs", Locked) is null) {
            Assert.Skip(Skip);
            return;
        }

        var (result, _) = Run(BaselineCommand.Verb.Create, scratch, false);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Contains("SK9015", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("firing now", result.Output, StringComparison.Ordinal);
    }
}
