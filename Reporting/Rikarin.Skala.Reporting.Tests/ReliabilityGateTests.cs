using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     The two statements a run makes about itself, and that no gate used to read.
/// </summary>
/// <remarks>
///     ⚠ Both are unconditional and named by no gate, which is what separates them from every other
///     condition in <see cref="Gate" />. The rest are opinions a repository opts into in
///     <c>skala.jsonc</c> — how severe is too severe, how many new findings are tolerable. These two say
///     the <em>denominator is unknown</em>: the run did not finish reading the tree (#309), or a rule
///     died on the first file and reported nothing for the rest (#295). A verdict computed over an
///     unknown fraction of a tree is not a verdict, so there is nothing to opt into.
/// </remarks>
public sealed class ReliabilityGateTests {
    static RunReport Report() => new() { RepositoryRoot = "/repo", Mode = LoadMode.Loose };

    /// <summary>
    ///     #309: a partial run fails the gate.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sabotage by removing the <c>EvaluateReliability</c> call from <c>Gate.Evaluate</c>. The
    ///     gate then passes — which is exactly what it did before: the flag was set, serialised into the
    ///     SARIF, printed as "⚠ partial run", and consulted by nothing at all.
    /// </remarks>
    [Fact]
    public void PartialRun_FailsTheGate() {
        var result = Gate.Evaluate(GateDefinition.Local, Report() with { Partial = true }, true);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, static failure => failure.Contains("partial", StringComparison.Ordinal));
    }

    /// <summary>
    ///     #309: the failure names the units that were cancelled, not just the fact of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The flag is a bare boolean aggregated with <c>|=</c> across compilation units, so a reader
    ///     could not tell one cancelled project from a Ctrl-C halfway through. A sweep reported
    ///     <c>partial: true</c> beside 211 findings and could not explain it; that took a hand trace,
    ///     which is the cost this assertion is here to remove.
    /// </remarks>
    [Fact]
    public void PartialRun_NamesTheCancelledUnits() {
        var report = Report() with {
            Partial = true,
            Diagnostics = [
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.PartialAnalysis,
                    SkalaSeverity.Warning,
                    "'Vixen.Water' was cancelled before it finished and contributed no findings",
                    "/repo/Core/Vixen.Water/Vixen.Water.csproj"
                )
            ]
        };

        var result = Gate.Evaluate(GateDefinition.Local, report, true);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Failures,
            static failure => failure.Contains("Vixen.Water.csproj", StringComparison.Ordinal)
        );
    }

    /// <summary>
    ///     #295: a crashed analyzer fails the gate.
    /// </summary>
    /// <remarks>
    ///     ⚠ The issue reported this as "the pipeline neither surfaces nor records AD0001", and that is
    ///     false — <c>AnalyzerHost</c>'s <c>onAnalyzerException</c> callback has always emitted
    ///     <c>SK9030</c>, the renderers print it and the SARIF carries it. The real defect is here: a
    ///     crashed analyzer is disabled for the rest of the run and contributes zero findings, and
    ///     nothing turned that into a verdict, so a dead rule and a quiet rule passed identically.
    ///     Sabotage by removing the <c>crashed</c> block from <c>Gate.EvaluateReliability</c>.
    /// </remarks>
    [Fact]
    public void CrashedAnalyzer_FailsTheGate() {
        var report = Report() with {
            Diagnostics = [
                new SkalaDiagnostic(
                    RuleIds.AnalyzerThrew,
                    SkalaSeverity.Warning,
                    "analyzer 'RedundantArgumentAnalyzer' threw on rule 'SK0232' and was disabled for the rest of the run",
                    "/repo/Widget.cs"
                )
            ]
        };

        var result = Gate.Evaluate(GateDefinition.Local, report, true);

        Assert.False(result.Passed);
        Assert.Contains(
            result.Failures,
            static failure => failure.Contains("RedundantArgumentAnalyzer", StringComparison.Ordinal)
        );
    }

    /// <summary>
    ///     #295: a source generator's own reported error is not a crash and does not fail the gate.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>GeneratorDriver</c> reports a generator's <em>reported</em> diagnostics under the same
    ///     id at <c>Info</c> — the generator talking about the code rather than falling over. Failing on
    ///     those would fail every repository whose build emits one, which is the unsatisfiable-gate
    ///     mistake this file's siblings document at length. Sabotage by dropping the severity test from
    ///     <c>EvaluateReliability</c>.
    /// </remarks>
    [Fact]
    public void GeneratorReportedDiagnostic_DoesNotFailTheGate() {
        var report = Report() with {
            Diagnostics = [
                new SkalaDiagnostic(
                    RuleIds.AnalyzerThrew,
                    SkalaSeverity.Info,
                    "a source generator reported CS8785: generator failed to generate a member",
                    "/repo/Widget.cs"
                )
            ]
        };

        Assert.True(Gate.Evaluate(GateDefinition.Local, report, true).Passed);
    }

    /// <summary>
    ///     #295: a gate input that could not be read is not an analyzer crash.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the assertion that makes the one above safe. Four unrelated conditions used to be
    ///     reported as <c>SK9030</c> — an unresolvable <c>--since</c>, a missing baseline, an unreadable
    ///     one, a failed suppression comparison — so failing the gate on <c>SK9030</c> would have failed
    ///     a run whose only problem is that a baseline file is absent, while telling its author an
    ///     analyzer had crashed. They are <c>SK9028</c>, and the reliability gate must ignore them.
    /// </remarks>
    [Fact]
    public void MissingGateInput_DoesNotFailTheReliabilityGate() {
        var report = Report() with {
            Diagnostics = [
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.GateInputUnavailable,
                    SkalaSeverity.Warning,
                    "the gate names a baseline at .skala/baseline.sarif and there is no such file",
                    "/repo/.skala/baseline.sarif"
                )
            ]
        };

        Assert.True(Gate.Evaluate(GateDefinition.Local, report, true).Passed);
    }

    /// <summary>A run that finished and crashed nothing still passes, so the gate is not vacuous.</summary>
    [Fact]
    public void CleanRun_StillPasses() => Assert.True(Gate.Evaluate(GateDefinition.Local, Report(), true).Passed);
}
