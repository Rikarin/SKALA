using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     The runs in which the tool did not do what it was asked and used to say nothing about it.
/// </summary>
/// <remarks>
///     ⚠ Four defects with one shape, and the shape is the reason they are tested together rather than
///     beside the code each one lives in. <c>--rules A,B</c> matched nothing and reported a clean tree
///     (#278); <c>--load=workspace</c> analysed the current repository instead of the path it was given
///     and reported that confidently (#284); a cancelled compilation unit marked the run partial and no
///     reader consulted the flag (#309); a crashed analyzer produced no findings and passed the gate
///     like a rule that found none (#295). Every one of them answers with a plausible number on the day
///     it did not run, which is the failure mode this repository's own evidence rules exist to catch.
///     <para>
///         ⚠ Each test here is written to go red when the fix is reverted, not merely to pass. That is
///         the property that matters: the defects were all *silences*, and a test that asserts a silence
///         is absent has to be provoked into failing at least once to be worth anything.
///     </para>
/// </remarks>
public sealed class InstrumentSilenceTests {
    const string Unformatted = """
                               public sealed class Widget
                               {
                                   public   int  Value {get;set;}
                               }
                               """;

    static CheckRequest LooseRequest(Scratch scratch, params string[] rules) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            AllowLoadFallback = false,
            Output = string.Empty,
            IncludeMetrics = false,
            Rules = rules
        };

    /// <summary>
    ///     #278: the comma spelling has to select the same rules the repeated spelling does.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sabotage by making <c>NormalizeRuleFilter</c> return its argument unchanged. The whole
    ///     string <c>"SK0001,SK0002"</c> then matches no finding's id, which is the original defect —
    ///     and the run no longer reports a clean tree, it is refused, because the same string also
    ///     matches no *known* id. Both halves of the fix are load-bearing for this assertion.
    /// </remarks>
    [Fact]
    public void RulesFilter_AcceptsTheCommaSpelling() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);

        var (repeated, repeatedReport) = CheckCommand.Run(
            LooseRequest(scratch, "SK0001", "SK0002"),
            TestContext.Current.CancellationToken
        );
        var (comma, commaReport) = CheckCommand.Run(
            LooseRequest(scratch, "SK0001,SK0002"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, comma.ExitCode);
        Assert.NotEmpty(commaReport.Findings);
        Assert.Contains(commaReport.Findings, static finding => finding.RuleId == "SK0001");
        Assert.Equal(repeated.ExitCode, comma.ExitCode);
        Assert.Equal(repeatedReport.Findings.Length, commaReport.Findings.Length);
    }

    /// <summary>
    ///     #278: a filter that cannot match anything is refused rather than reported as a clean tree.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the half that survives the comma fix. A mistyped id reaches the identical false
    ///     clean by another route, and it is the route a person actually takes — <c>SK3150</c> for
    ///     <c>SK3510</c>. Sabotage by deleting the <c>unknownRules.Length == request.Rules.Count</c>
    ///     branch in <c>CheckCommand.Run</c>: the run then exits 0 with no findings and no complaint.
    /// </remarks>
    [Fact]
    public void RulesFilter_RefusesAFilterNoRuleCanSatisfy() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);

        var (result, _) = CheckCommand.Run(
            LooseRequest(scratch, "SK9999", "SK9998"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains("SK9999", result.Output, StringComparison.Ordinal);
        Assert.Contains("clean tree", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #278: a filter naming one live id and one dead one still measures something, so it warns.
    /// </summary>
    [Fact]
    public void RulesFilter_WarnsWhenOnlyPartOfTheFilterIsKnown() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);

        var (result, report) = CheckCommand.Run(
            LooseRequest(scratch, "SK0001,SK9999"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.Contains(
            report.Diagnostics,
            static diagnostic => diagnostic.Id == ConfigDiagnosticIds.UnknownRuleFilter
        );
    }

    /// <summary>
    ///     #284: the tree analysed must be the tree requested.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is about <em>which directory</em> the resolution names, because that is what
    ///     the defect got wrong — the loader returned a perfectly good solution belonging to somebody
    ///     else. Sabotage by restoring <c>Resolve</c> to glob <c>request.RepositoryRoot</c> and ignore
    ///     <c>request.Paths</c>: this returns the repository's own project instead of the probe's and
    ///     the equality fails.
    /// </remarks>
    [Fact]
    public void WorkspaceResolve_PrefersAProjectUnderTheRequestedPath() {
        using var repository = new Scratch();
        using var probe = new Scratch();
        repository.Write("Repository.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var expected = probe.Write("Probe.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");

        var resolution = WorkspaceLoader.Resolve(
            new LoadRequest { RepositoryRoot = repository.Root, Mode = LoadMode.Workspace, Paths = [probe.Root] }
        );

        Assert.Null(resolution.Error);
        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(resolution.Target!));
    }

    /// <summary>
    ///     #284: with no project under a path outside the root, the loader refuses instead of falling back.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the reproduction from the issue, and the one that cost the most: a probe project
    ///     dropped outside the checkout, swept under <c>--load=workspace</c> from inside it, returned a
    ///     clean report about Skala. Nothing named the tree that had been read. Sabotage by returning
    ///     <c>SearchIn(root)</c> here instead of the refusal — the resolution then names the
    ///     repository's own project and the test's <c>Assert.Null(Target)</c> fails.
    /// </remarks>
    [Fact]
    public void WorkspaceResolve_RefusesToFallBackToADifferentTree() {
        using var repository = new Scratch();
        using var probe = new Scratch();
        repository.Write("Repository.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        probe.Write("Probe.cs", "public sealed class Probe { }");

        var resolution = WorkspaceLoader.Resolve(
            new LoadRequest { RepositoryRoot = repository.Root, Mode = LoadMode.Workspace, Paths = [probe.Root] }
        );

        Assert.Null(resolution.Target);
        Assert.NotNull(resolution.Error);
        Assert.Contains("Refusing to fall back", resolution.Error, StringComparison.Ordinal);

        // ⚠ `auto` must still reach loose rather than choosing workspace and hard-failing: for
        // discovery this refusal means "there is genuinely no workspace target here", which is the
        // documented condition for loose — and loose honours the path.
        Assert.False(resolution.ShouldAttemptWorkspace);
    }

    /// <summary>
    ///     #284: a subdirectory of the repository still loads the repository's solution.
    /// </summary>
    /// <remarks>
    ///     ⚠ The fallback is kept exactly where it cannot change which tree is measured.
    ///     <c>check src --load=workspace</c> — load the solution, report on a subtree — is an ordinary
    ///     workflow, and refusing there would have traded one defect for a worse one.
    /// </remarks>
    [Fact]
    public void WorkspaceResolve_StillFallsBackWithinTheSameRepository() {
        using var repository = new Scratch();
        var expected = repository.Write("Repository.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        repository.Write(Path.Combine("src", "Widget.cs"), "public sealed class Widget { }");

        var resolution = WorkspaceLoader.Resolve(
            new LoadRequest {
                RepositoryRoot = repository.Root,
                Mode = LoadMode.Workspace,
                Paths = [Path.Combine(repository.Root, "src")]
            }
        );

        Assert.Null(resolution.Error);
        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(resolution.Target!));
    }

    /// <summary>
    ///     #346: one <c>.cs</c> file on the command line, one file in the report — under <c>loose</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is the mode that was already right, and it is here as the control.</b> The issue
    ///     predicts it stays green through the sabotage, and it does: <c>LooseLoader.Collect</c> reads
    ///     <c>request.Paths</c> and parses that one file, so the loaded set and the reported set
    ///     coincide before any filter runs. Measured through the CLI on a two-project scratch tree:
    ///     <c>loose</c> reported 1 file where <c>workspace</c> reported 4. A fixture that only covered
    ///     <c>loose</c> would therefore have been green on the day the defect shipped — which is why
    ///     the workspace twin of this test lives in <c>WorkspaceLoadingTests</c> and is the one that
    ///     actually holds the fix down.
    /// </remarks>
    [Fact]
    public void RequestedFile_IsTheOnlyFileReportedUnderLoose() {
        using var scratch = new Scratch();
        var file = scratch.Write("Widget.cs", Unformatted);
        scratch.Write("Other.cs", Unformatted.Replace("Widget", "Other", StringComparison.Ordinal));

        var (result, report) = CheckCommand.Run(
            LooseRequest(scratch) with { Paths = [file] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.NotEmpty(report.Findings);
        Assert.All(report.Findings, finding => Assert.Equal(file, finding.Path));
        Assert.Equal(1, report.FileCount);
    }

    /// <summary>
    ///     #346: a requested path no document of this load carries is refused, not reported as clean.
    /// </summary>
    /// <remarks>
    ///     ⚠ The other half of the fix, and the one that keeps it from trading a superset for a false
    ///     clean. Once every producer is filtered by the requested paths, a path that matches no
    ///     document filters everything away and the run exits 0 with an empty report — the same silence
    ///     #278 reached through an unmatched <c>--rules</c>, arrived at through an unmatched path.
    ///     Sabotage by deleting the <c>scope is { Count: 0 }</c> branch in <c>CheckCommand.Run</c>: this
    ///     goes green-and-empty at <c>ExitCodes.Ok</c>.
    ///     <para>
    ///         ⚠ The fixture is a directory holding one <c>.g.cs</c> and nothing else, because that is
    ///         the condition reduced to something a fixture can carry: the loader parses it — the load
    ///         is not empty and does not fail — and <c>ReportablePaths</c> excludes it, so the load
    ///         succeeds and the reported set is empty. A path with no <c>.cs</c> under it at all is a
    ///         different code path that already fails as <c>LoadFailure</c>.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RequestedPath_ThatNoLoadedDocumentCarriesIsRefused() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);
        scratch.Write(Path.Combine("Generated", "Widget.g.cs"), Unformatted);

        // The control: the same tree, asked about itself, reports normally.
        var (whole, report) = CheckCommand.Run(LooseRequest(scratch), TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Ok, whole.ExitCode);
        Assert.NotEmpty(report.Findings);

        var (refused, _) = CheckCommand.Run(
            LooseRequest(scratch) with { Paths = [Path.Combine(scratch.Root, "Generated")] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, refused.ExitCode);
        Assert.Contains("clean tree", refused.Output, StringComparison.Ordinal);
        Assert.Contains("--load=loose", refused.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #346, found while fixing it: a trailing directory separator silenced the formatting half.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A second, independent defect, and it had been shipping.</b>
    ///     <c>Path.GetFullPath("Alpha/")</c> keeps the separator, so <c>CheckCommand.Paths</c> compared
    ///     files against <c>.../Alpha/</c> and <c>.../Alpha//</c> and matched none of them. Measured
    ///     through the CLI before the trim went in: <c>check Alpha/ --load=loose</c> reported the
    ///     analyzer findings for all three files and <em>no SK0001 at all</em>, while
    ///     <c>check Alpha</c> reported SK0001 for each of them. The formatting, arrangement and
    ///     duplication halves were each handed an empty set and each returned a zero that reads exactly
    ///     like a formatted tree. A shell's tab-completion appends that separator.
    ///     <para>
    ///         ⚠ It also had to be fixed <em>with</em> #346 rather than after it: the refusal above
    ///         turns "no file matched" into an error, so leaving the trim out would have converted a
    ///         quietly wrong run into a refused one for an ordinary command line.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RequestedDirectory_MatchesWithOrWithoutATrailingSeparator() {
        using var scratch = new Scratch();
        scratch.Write(Path.Combine("Alpha", "Widget.cs"), Unformatted);
        var directory = Path.Combine(scratch.Root, "Alpha");

        var (_, bare) = CheckCommand.Run(
            LooseRequest(scratch) with { Paths = [directory] },
            TestContext.Current.CancellationToken
        );
        var (_, trailing) = CheckCommand.Run(
            LooseRequest(scratch) with { Paths = [directory + Path.DirectorySeparatorChar] },
            TestContext.Current.CancellationToken
        );

        Assert.Contains(bare.Findings, static finding => finding.RuleId == RuleIds.FileIsNotFormatted);
        Assert.Equal(
            bare.Findings.Select(static finding => finding.RuleId).Order(StringComparer.Ordinal),
            trailing.Findings.Select(static finding => finding.RuleId).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    ///     #305: <c>-o report.sarif</c> has no directory to create, and used to throw after the run.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Path.GetDirectoryName</c> returns the empty string — not <c>null</c> — for a bare
    ///     filename, and <c>Directory.CreateDirectory("")</c> throws <c>ArgumentException</c>. It failed
    ///     at the very end, after the whole analysis was paid for. Sabotage by restoring the
    ///     <c>parent is null</c> guard: this throws.
    ///     <para>
    ///         ⚠ No <c>Directory.SetCurrentDirectory</c>, deliberately. The working directory is
    ///         process-global and xUnit runs test classes in parallel, so a test that moves it moves it
    ///         under every other test in the assembly. The contract under test needs no directory
    ///         anyway — the correct behaviour for a bare filename is to create nothing at all.
    ///     </para>
    /// </remarks>
    [Fact]
    public void EnsureForFile_AcceptsABareFilename() {
        Assert.Null(Record.Exception(static () => SkalaDirectory.EnsureForFile("report.sarif")));
        Assert.Null(Record.Exception(static () => SkalaDirectory.EnsureForFile("report.json")));
    }

    /// <summary>
    ///     #295: the four conditions that were reported as "an analyzer threw" no longer are.
    /// </summary>
    /// <remarks>
    ///     ⚠ The point is not the number but the disambiguation. While a missing baseline shared
    ///     <c>SK9030</c> with an analyzer crash, <c>SK9030</c> in a report could not answer "did a rule
    ///     die" — and the gate could not be made to fail on a crash without also failing on a baseline
    ///     file that is merely absent. Sabotage by putting <c>RuleIds.AnalyzerThrew</c> back on the
    ///     missing-baseline diagnostic in <c>CheckCommand.Scope</c>.
    /// </remarks>
    [Fact]
    public void MissingBaseline_IsNotReportedAsACrashedAnalyzer() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);

        var (_, report) = CheckCommand.Run(
            LooseRequest(scratch) with { BaselinePath = Path.Combine(scratch.Root, "absent.sarif") },
            TestContext.Current.CancellationToken
        );

        Assert.Contains(
            report.Diagnostics,
            static diagnostic => diagnostic.Id == ConfigDiagnosticIds.GateInputUnavailable
        );

        Assert.DoesNotContain(report.Diagnostics, static diagnostic => diagnostic.Id == RuleIds.AnalyzerThrew);
    }

    /// <summary>
    ///     ⚠ #336, the fifth of the shape: a source generator assembly that is not on disk cost its
    ///     entire output and <c>GeneratorDriver</c> said nothing at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ Worse than the four above, because the compilation that comes back is not smaller but
    ///     <em>different</em>: every generated member is absent, and a semantic rule then answers
    ///     confidently about a program the repository does not contain. On this repository the
    ///     measured cost was 353 rewrites that do not compile.
    ///     <para>
    ///         Sabotage: put back the bare <c>if (!File.Exists(path)) continue;</c> in
    ///         <c>GeneratorDriver.Run</c> — that is, drop the <c>ReportMissingAssemblies</c> call above
    ///         it — and this goes red.
    ///     </para>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             It goes through <c>Run</c> rather than calling the helper, and the first draft did
    ///             not.
    ///         </b> Calling <c>ReportMissingAssemblies</c> directly asserts that the helper works,
    ///         which was never in doubt; the defect was the *call site*, and that draft survived its own
    ///         sabotage untouched. Found by running the sabotage rather than by reading the test, which
    ///         is the whole argument for running it.
    ///     </para>
    /// </remarks>
    [Fact]
    public void MissingGeneratorAssembly_IsReported() {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var compilation = CSharpCompilation.Create(
            "Scratch",
            [
                CSharpSyntaxTree.ParseText(
                    "internal sealed class Widget;",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ]
        );

        var result = GeneratorDriver.Run(
            compilation,
            [Path.Combine(Path.GetTempPath(), "skala-absent", "Absent.Generator.dll")],
            [],
            null,
            new(),
            diagnostics,
            TestContext.Current.CancellationToken
        );

        Assert.Same(compilation, result);
        var reported = Assert.Single(diagnostics);
        Assert.Equal(ConfigDiagnosticIds.AnalyzerAssemblyMissing, reported.Id);
        Assert.Equal(SkalaSeverity.Warning, reported.Severity);
        Assert.Contains("Absent.Generator.dll", reported.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And a present assembly is not reported, which is the half that keeps the guard honest.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>SK9031</c> covers an assembly that <em>is</em> there and throws, and is deliberately
    ///     never fatal.
    ///     <b>
    ///         Absence is a different fact and this test is what stops the two from being
    ///         collapsed
    ///     </b>: it passes an existing file that is certainly not a generator, and requires
    ///     silence. A guard that reported "this is not a generator" here would refuse every load in
    ///     which any analyzer contributes no source, which is most of them.
    /// </remarks>
    [Fact]
    public void PresentAssembly_IsNotReportedAsMissing() {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var missing = GeneratorDriver.ReportMissingAssemblies(
            [typeof(InstrumentSilenceTests).Assembly.Location],
            "Scratch",
            diagnostics,
            SkalaSeverity.Error
        );

        Assert.False(missing);
        Assert.Empty(diagnostics);
    }
}
