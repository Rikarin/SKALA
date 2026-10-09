using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     ⚠ #592: arrangement findings are not baseline material, including the ones a baseline already
///     holds from before the rule.
/// </summary>
/// <remarks>
///     <para>
///         The CLI half — <c>update</c> names and refuses, <c>verify --baseline</c> still fails — is
///         <c>Rikarin.Skala.Cli.Tests.BaselineArrangementTests</c>. This half is the entry an earlier
///         <c>baseline update</c> already wrote, which only a hand-built file can reach now that nothing
///         writes one: it must stop suppressing its finding, must not be reported <em>fixed</em> by a
///         <c>check</c> that never looked for it, and the next writing verb drops it out loud.
///     </para>
///     <para>
///         ⚠ <b>Sabotage:</b> remove the <c>Without</c> from <c>CheckCommand.Scope</c> and
///         <see cref="ALegacyArrangementEntry_NoLongerSuppressesNorReadsAsFixed" /> goes red on both
///         halves; remove it from <c>BaselineCommand</c> and
///         <see cref="ALegacyArrangementEntry_IsDroppedByUpdate_AndSaid" /> does.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class BaselineArrangementTests {
    /// <summary><c>using static</c> out of order: the issue's own shape, and syntactic.</summary>
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

    static CheckRequest Request(Scratch scratch, bool arrangement) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            IncludeArrangement = arrangement,
            IncludeHints = true,
            NoCache = true
        };

    /// <summary>What <c>baseline update</c> wrote before #592: every finding, arrangement included.</summary>
    static string WriteLegacyBaseline(Scratch scratch) {
        var (_, report) = CheckCommand.Run(Request(scratch, true), TestContext.Current.CancellationToken);
        var path = Path.Combine(scratch.Root, ".skala", "baseline.sarif");
        Baseline.Write(path, report, report.Findings);

        // The premise: the file really does hold an arrangement entry.
        Assert.Contains(Baseline.Read(path).Entries, static entry => ArrangementFindings.Owns(entry.RuleId));
        return path;
    }

    [Fact]
    public void ALegacyArrangementEntry_NoLongerSuppressesNorReadsAsFixed() {
        using var scratch = new Scratch();
        scratch.Write("Calculator.cs", Unsorted);
        var path = WriteLegacyBaseline(scratch);

        // The run that collects arrangement — `verify`'s — sees the finding as new, not accepted.
        var (_, arranged) = CheckCommand.Run(
            Request(scratch, true) with { BaselinePath = path },
            TestContext.Current.CancellationToken
        );

        Assert.Contains(arranged.New, static finding => ArrangementFindings.Owns(finding.RuleId));

        // The run that does not — `check --gate=ci`'s — must not report it as fixed either. That is
        // doc 09's `SK7020` misreport ("308 fixed; nothing had been fixed, a rule had not run") again.
        var (_, analysed) = CheckCommand.Run(
            Request(scratch, false) with { BaselinePath = path },
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(analysed.Fixed, static entry => ArrangementFindings.Owns(entry.RuleId));
    }

    [Fact]
    public void ALegacyArrangementEntry_IsDroppedByUpdate_AndSaid() {
        using var scratch = new Scratch();
        scratch.Write("Calculator.cs", Unsorted);
        var path = WriteLegacyBaseline(scratch);

        var (result, _) = BaselineCommand.Run(
            BaselineCommand.Verb.Update,
            Request(scratch, true) with { BaselinePath = path },
            true,
            TestContext.Current.CancellationToken
        );

        Assert.Contains("are dropped", result.Output, StringComparison.Ordinal);
        Assert.Contains("not arranged", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Baseline.Read(path).Entries, static entry => ArrangementFindings.Owns(entry.RuleId));
    }

    /// <summary>
    ///     ⚠ The id set is the arranger's, and only the arranger's: the <c>SK023x</c>+ cleanup analyzers
    ///     are ordinary findings and stay baselineable.
    /// </summary>
    [Fact]
    public void TheRefusedIds_AreTheArrangersAndNoAnalyzers() {
        Assert.Contains("SK0210", ArrangementFindings.RuleIds);
        Assert.Contains("SK0219", ArrangementFindings.RuleIds);

        // `SK0230` is a cleanup analyzer `check` reports with a fix; it is a finding like any other.
        Assert.DoesNotContain("SK0230", ArrangementFindings.RuleIds);
        Assert.DoesNotContain(
            ArrangementFindings.RuleIds,
            static id => !id.StartsWith("SK02", StringComparison.Ordinal)
        );
    }
}
