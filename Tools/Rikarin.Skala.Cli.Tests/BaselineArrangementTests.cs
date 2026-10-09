using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     ⚠ #592: <c>baseline update</c> accepted five <c>SK0210</c> findings into
///     <c>.skala/baseline.sarif</c>, <c>check --gate=ci</c> passed, and Lint's <c>arrange --check</c>
///     stayed red on the same files.
/// </summary>
/// <remarks>
///     ⚠ The accepted entries never hid anything from <c>check</c>, which does not collect arrangement
///     at all; what they did was report a red Lint as accepted, and suppress the arrange half of
///     <c>verify --baseline</c>, the one place a baseline met an arrangement finding. The contract is
///     now that an arrangement finding is not baseline material: <c>arrange</c> fixes it or
///     <c>.editorconfig</c> switches it off. Driven through the real binary because the defect was in
///     what three commands said to each other, not in any one of them.
/// </remarks>
public sealed class BaselineArrangementTests : IDisposable {
    /// <summary>
    ///     The issue's shape: <c>using static</c> directives out of order. ⚠ Sorting is syntactic, so it
    ///     fires under <c>--load=loose</c>; only removing an unused using needs a compilation, and both
    ///     directives are used so that nothing but the order is wrong.
    /// </summary>
    const string Unsorted = """
                            using static System.Math;
                            using static System.Console;

                            namespace Scratch;

                            public static class Calculator {
                                public static void Print(double value) {
                                    WriteLine(Sqrt(value));
                                }
                            }

                            """;

    const string Loose = "--load=loose";

    readonly CrossPlatformScratch scratch = new("skala-baseline-arrange-");

    public void Dispose() => scratch.Dispose();

    string Baseline => Path.Combine(scratch.Root, ".skala", "baseline.sarif");

    static string Describe(CliRun run) => "exit=" + run.ExitCode + "\n" + run.StandardOutput + "\n" + run.StandardError;

    [Fact]
    public void BaselineUpdate_DoesNotAcceptAnArrangementFinding_AndSaysSo() {
        scratch.InitialiseGit();
        scratch.WriteText("Calculator.cs", Unsorted);

        // The premise: Lint's command fails on this file. Without it the rest proves nothing.
        var arrange = scratch.Run("arrange", "--check", Loose, ".");
        Assert.True(arrange.ExitCode == ExitCodes.FormattingNeeded, "arrange --check: " + Describe(arrange));

        var update = scratch.Run("baseline", "update", Loose, "--apply", ".");
        Assert.True(update.ExitCode == ExitCodes.Ok, "baseline update: " + Describe(update));

        // Named, not silently dropped: the silence was half of the defect.
        Assert.Contains("not arranged", update.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK0210", update.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Calculator.cs", update.StandardOutput, StringComparison.Ordinal);

        Assert.True(File.Exists(Baseline), "baseline update wrote nothing: " + Describe(update));
        // ⚠ The result's `ruleId`, not the id anywhere: the `rules` table of any Skala SARIF lists
        // `SK0210` whether or not a result carries it.
        Assert.DoesNotContain("\"ruleId\": \"SK0210\"", File.ReadAllText(Baseline), StringComparison.Ordinal);

        // And `arrange --check` is still what it was — the baseline was never its business.
        Assert.Equal(ExitCodes.FormattingNeeded, scratch.Run("arrange", "--check", Loose, ".").ExitCode);
    }

    /// <summary>
    ///     ⚠ The command where an accepted arrangement finding actually hid something: <c>verify</c> is
    ///     <c>arrange --check</c> plus two other stages, and with <c>--baseline</c> it said "nothing to
    ///     do" over a file Lint rejects.
    /// </summary>
    [Fact]
    public void VerifyWithABaseline_StillReportsTheUnarrangedFile() {
        scratch.InitialiseGit();
        scratch.WriteText("Calculator.cs", Unsorted);

        // Formatting is the formatter's business and stays baselineable; take it off the table so
        // that the only thing `verify` can be failing on is the arrangement.
        var format = scratch.Run("format", ".");
        Assert.True(format.ExitCode == ExitCodes.Ok, "format: " + Describe(format));

        var create = scratch.Run("baseline", "create", Loose, "--apply", ".");
        Assert.True(create.ExitCode == ExitCodes.Ok, "baseline create: " + Describe(create));

        var verify = scratch.Run("verify", Loose, "--baseline", Baseline, ".");
        Assert.True(verify.ExitCode == ExitCodes.GateFailed, "verify --baseline: " + Describe(verify));
        Assert.Contains("skala arrange", verify.StandardOutput, StringComparison.Ordinal);

        // Anti-vacuity: once arranged, the same baseline does leave `verify` with nothing to do, so the
        // failure above was the arrangement and not something else the baseline missed.
        var apply = scratch.Run("arrange", Loose, ".");
        Assert.True(apply.ExitCode == ExitCodes.Ok, "arrange: " + Describe(apply));

        var after = scratch.Run("verify", Loose, "--baseline", Baseline, ".");
        Assert.True(after.ExitCode == ExitCodes.Ok, "verify --baseline after arrange: " + Describe(after));
    }
}
