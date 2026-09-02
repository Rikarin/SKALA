using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules;
using Rikarin.Skala.Rules.Metadata;
using System.Diagnostics;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>The host, the cache, and the two commands an agent runs.</summary>
public sealed class AnalysisTests {
    const string NeedsModernizing = """
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

    static CheckRequest Request(Scratch scratch) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Loose,
            Output = string.Empty,
            NoCache = true
        };

    /// <summary>
    ///     Every analyzer in the package must be instantiated by the built-in host.
    /// </summary>
    /// <remarks>
    ///     ⚠ The package discovers analyzer classes, while <c>skala check</c> runs an explicit instance
    ///     list. Without this the two can disagree and a fully tested analyzer can ship in the package
    ///     while the CLI silently never runs it.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             This remark used to claim the fixture harness discovered analyzers too, and it did
    ///             not
    ///         </b> — <c>RuleFixtureTests</c> held a second hand-written copy of the same 290
    ///         instances, and a rule missing from it would have been measured by a set that is not the
    ///         set that ships. Both lists are now <see cref="SkalaAnalyzers.All" />, so this assertion
    ///         covers the harness as well as the CLI (#297).
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnalyzerHost_OwnsEveryAnalyzerInTheRulesAssembly() {
        var declared = typeof(SkalaRule).Assembly.GetTypes()
            .Where(static type => type is { IsAbstract: false, IsPublic: true }
                && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null
            )
            .Select(static type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var hosted = AnalyzerHost.Own.Select(static analyzer => analyzer.GetType().FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, hosted);
    }

    /// <summary>
    ///     ⚠ In loose mode there is no project, so half the references are missing and CS0246 is the
    ///     expected state. Reporting the compiler's opinion there would bury the findings the mode
    ///     exists to produce under complaints about code that is fine.
    /// </summary>
    [Fact]
    public void Check_InLooseMode_ReportsNoCompilerDiagnostics() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo { public SomeTypeFromAPackage Value; }");

        var (_, report) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            report.Findings,
            static finding => finding.RuleId.StartsWith("CS", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Check_InLooseMode_RunsTheSyntacticRules() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsModernizing);

        var (_, report) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);

        Assert.Equal(LoadMode.Loose, report.Mode);
        Assert.Contains(report.Findings, static finding => finding.RuleId == RuleIds.FileScopedNamespace);
        Assert.Contains(report.Findings, static finding => finding.RuleId == RuleIds.NullCoalescingAssignment);
    }

    /// <summary>
    ///     ⚠ docs/plan/07 § loose: the mode "is honest, because the SARIF says loadMode: loose and lists
    ///     the rules that were skipped". A report that omits this is a report whose clean result means
    ///     something different from another clean result.
    /// </summary>
    [Fact]
    public void Check_InLooseMode_ListsEverySemanticRuleAsSkipped() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsModernizing);

        var (_, report) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);
        var skipped = report.SkippedRules.Select(static rule => rule.RuleId).ToHashSet(StringComparer.Ordinal);

        // ⚠ `!rule.Retired`, matching AnalyzerHost's own filter. A retired rule is not "skipped
        // because there is no compilation" — it is not run at all, and listing it here would tell a
        // consumer it would have fired with a project, which is the one thing that is not true of it.
        foreach (var rule in RuleCatalog.All.Where(static rule => rule.RequiresSemantics && !rule.Retired)) {
            Assert.Contains(rule.Id, skipped);
        }

        foreach (var rule in RuleCatalog.All.Where(static rule => rule.Retired)) {
            Assert.DoesNotContain(rule.Id, skipped);
        }

        Assert.DoesNotContain(RuleIds.FileScopedNamespace, skipped);
    }

    [Fact]
    public void Check_ReportsFormattingAsSK0001WithTheFormattersOwnEdits() {
        using var scratch = new Scratch();
        scratch.Write("Ugly.cs", "public sealed class Ugly{public int    Value;}");

        var (_, report) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);
        var formatting = report.Findings.Single(static finding => finding.RuleId == RuleIds.FileIsNotFormatted);

        Assert.True(formatting.HasFix);
        Assert.True(formatting.FixIsSafe);
    }

    [Fact]
    public void Verify_ExitsZeroOnlyWhenThereIsNothingToDo() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsModernizing);

        var dirty = VerifyCommand.Run(
            new VerifyRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], NoCache = true },
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(ExitCodes.Ok, dirty.ExitCode);

        using var clean = new Scratch();
        clean.Write("Clean.cs", "namespace Scratch;\n\npublic sealed class Clean;\n");

        var result = VerifyCommand.Run(
            new VerifyRequest { RepositoryRoot = clean.Root, Paths = [clean.Root], NoCache = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
    }

    [Fact]
    public void Verify_IncludesArrangeCheckWithoutWriting() {
        using var scratch = new Scratch();
        var path = scratch.Write(
            "Holder.cs",
            "namespace Scratch;\n\npublic sealed class Holder {\n    public int Value() {\n        return 1;\n    }\n}\n"
        );
        var before = File.ReadAllText(path);

        var result = VerifyCommand.Run(
            new VerifyRequest {
                RepositoryRoot = scratch.Root, Paths = [scratch.Root], Mode = LoadMode.Loose, NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.GateFailed, result.ExitCode);
        Assert.Contains(ArrangeIds.BodyStyle, result.Output, StringComparison.Ordinal);
        Assert.Contains("skala arrange Holder.cs", result.Output, StringComparison.Ordinal);
        Assert.Contains("SKIPPED", result.Output, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Fix_AppliesTheSafeFixesAndFormatsAfterwards() {
        using var scratch = new Scratch();
        var path = scratch.Write("Holder.cs", NeedsModernizing);

        FixCommand.Run(
            new FixRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root] },
            TestContext.Current.CancellationToken
        );

        var after = File.ReadAllText(path);
        Assert.Contains("namespace Scratch;", after, StringComparison.Ordinal);
        Assert.Contains("??=", after, StringComparison.Ordinal);
        Assert.DoesNotContain("_items ?? new", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ docs/plan/10: "without --safe you must name the rules, which makes the choice visible in
    ///     its transcript."
    /// </summary>
    [Fact]
    public void Fix_WithoutSafeAndWithoutNamedRules_Refuses() {
        using var scratch = new Scratch();
        scratch.Write("Holder.cs", NeedsModernizing);

        var result = FixCommand.Run(
            new FixRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], SafeOnly = false },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
    }

    [Fact]
    public void Fix_DryRun_WritesNothing() {
        using var scratch = new Scratch();
        var path = scratch.Write("Holder.cs", NeedsModernizing);
        var before = File.ReadAllText(path);

        FixCommand.Run(
            new FixRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], DryRun = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Fix_IDE1006RejectsAnExplicitLooseLoad() {
        using var scratch = new Scratch();
        scratch.Write("bad_name.cs", "public sealed class bad_name;\n");

        var result = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Loose,
                SafeOnly = false,
                Include = [RoslynCodeStyle.NamingDiagnosticId]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains("omit --load", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_IDE1006IsNeverIncludedInSafeMode() {
        using var scratch = new Scratch();
        scratch.Write("bad_name.cs", "public sealed class bad_name;\n");

        var result = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                SafeOnly = true,
                Include = [RoslynCodeStyle.NamingDiagnosticId]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.ConfigurationError, result.ExitCode);
        Assert.Contains("never part of --safe", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The cache's whole reason for existing, and its whole risk.
    /// </summary>
    /// <remarks>
    ///     ⚠ A stale finding looks exactly like a real one and a missing finding looks exactly like a
    ///     clean file, so the cache is only allowed to be a speed-up if a second run over a changed
    ///     tree produces byte-identical findings to a run with no cache at all.
    /// </remarks>
    [Fact]
    public void Cache_ASecondRunAgreesWithAnUncachedOne() {
        using var scratch = new Scratch();
        scratch.Write("A.cs", NeedsModernizing);
        scratch.Write("B.cs", "namespace Other;\n\npublic sealed class Clean;\n");

        var cached = Request(scratch) with { NoCache = false };
        CheckCommand.Run(cached, TestContext.Current.CancellationToken);

        // Change one file; the other must come back from the cache and must come back the same.
        scratch.Write("B.cs", "namespace Other {\n    public sealed class Moved;\n}\n");

        var (_, warm) = CheckCommand.Run(cached, TestContext.Current.CancellationToken);
        var (_, cold) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);

        Assert.Equal(Describe(cold), Describe(warm));
    }

    /// <summary>
    ///     ⚠ docs/plan/07 § "Suppression", mechanism 3: <c>dotnet_diagnostic.SK1005.severity = none</c>
    ///     in a scoped section is the right way to turn a rule off for a folder. Roslyn reads it from
    ///     the compilation's <c>SyntaxTreeOptionsProvider</c>, which a hand-built compilation does not
    ///     have unless something puts one there.
    /// </summary>
    [Fact]
    public void EditorConfigSeverities_TurnARuleOff() {
        using var scratch = new Scratch();
        scratch.Write(".editorconfig", "root = true\n");
        scratch.Write("A.cs", NeedsModernizing);

        var (_, before) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);
        Assert.Contains(before.Findings, static finding => finding.RuleId == RuleIds.FileScopedNamespace);

        scratch.Write(
            ".editorconfig",
            "root = true\n\n[*.cs]\ndotnet_diagnostic.SK1005.severity = none\n"
        );

        Core.Configuration.ConfigurationCache.Clear();

        var (_, after) = CheckCommand.Run(Request(scratch), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(after.Findings, static finding => finding.RuleId == RuleIds.FileScopedNamespace);
    }

    /// <summary>
    ///     ⚠ The cache key carries the .editorconfig's text, so a severity change in a scoped section
    ///     invalidates it. Hashing the *resolved global* view instead would leave every key unmoved when
    ///     only a scoped section changed, which is a stale finding by construction.
    /// </summary>
    [Fact]
    public void Cache_IsInvalidatedByAnEditorConfigChange() {
        using var scratch = new Scratch();
        scratch.Write(".editorconfig", "root = true\n");
        scratch.Write("A.cs", NeedsModernizing);

        var request = Request(scratch) with { NoCache = false };
        var (_, first) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.Contains(RuleIds.FileScopedNamespace, Describe(first), StringComparison.Ordinal);

        scratch.Write(
            ".editorconfig",
            "root = true\n\n[*.cs]\ndotnet_diagnostic.SK1005.severity = none\n"
        );

        Core.Configuration.ConfigurationCache.Clear();

        var (_, second) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(RuleIds.FileScopedNamespace, Describe(second), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The cache's correctness condition. A compilation-scoped rule's answer for A.cs depends on
    ///     files the key for A.cs does not name, so it may never be stored per file.
    /// </summary>
    [Fact]
    public void Cache_NeverStoresACompilationScopedRulesFindings() {
        Assert.NotEmpty(DiagnosticCache.Uncacheable);

        foreach (var rule in RuleCatalog.All) {
            Assert.Equal(rule.Scope == RuleScope.Compilation, DiagnosticCache.Uncacheable.Contains(rule.Id));
        }
    }

    [Fact]
    public void Merge_KeepsTheTargetFrameworkListSoAOneTargetFindingLooksLikeOne() {
        var one = new Finding {
            RuleId = "SK1010",
            Severity = SkalaSeverity.Info,
            Message = "m",
            Path = "/a.cs",
            Line = 1,
            Column = 1,
            TargetFrameworks = ["net10.0"]
        };

        var both = AnalyzerHost.Merge([one, one with { TargetFrameworks = ["netstandard2.0"] }]);
        var single = AnalyzerHost.Merge([one]);

        Assert.Equal(["net10.0", "netstandard2.0"], Assert.Single(both).TargetFrameworks);
        Assert.Equal(["net10.0"], Assert.Single(single).TargetFrameworks);
    }

    /// <summary>
    ///     ⚠ docs/plan/08 § `supersedes`: one span, one finding, and which one wins is deterministic and
    ///     documented. The loser stays in the report, marked suppressed, so the SARIF still records that
    ///     the other analyzer had an opinion.
    /// </summary>
    [Fact]
    public void Supersession_DropsTheSupersededRuleAndKeepsItInTheReport() {
        var skala = new Finding {
            RuleId = RuleIds.FileScopedNamespace,
            Severity = SkalaSeverity.Info,
            Message = "skala",
            Path = "/a.cs",
            Line = 3,
            Column = 1
        };

        var roslyn = skala with { RuleId = "IDE0161", Message = "roslyn" };
        var result = Supersession.Apply([skala, roslyn]);

        Assert.Equal(
            SuppressionKind.None,
            result.Single(static f => f.RuleId == RuleIds.FileScopedNamespace).Suppression
        );
        Assert.Equal(SuppressionKind.Superseded, result.Single(static f => f.RuleId == "IDE0161").Suppression);
    }

    /// <summary>
    ///     ⚠ <b>Supersession is exact, and a claim whose spans do not coincide suppresses nothing.</b>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The mechanism pairs on <c>(rule, path, line, column)</c> and nothing measured whether a
    ///         given <c>supersedes</c> claim ever matches, so an inert claim looked exactly like a
    ///         working one — and read as coverage. Probed against the SDK on one file ([#314]):
    ///         <c>IDE0019</c> lands on the declaration and <c>SK1050</c> reports on the null check a line
    ///         below; <c>IDE0020</c> lands on the declaration and <c>SK1015</c> reports on the
    ///         <c>is</c> test a line above. Neither pair has ever suppressed anything.
    ///     </para>
    ///     <para>
    ///         ⚠ This pins the semantics rather than a defect. Widening the match to a line, to an
    ///         overlap or to a proximity window was rejected: the measured pairs share neither line nor
    ///         span, so nothing short of a guess would join them, and a wrong guess deletes a true
    ///         finding from another analyzer. <c>supersedes</c> is therefore read as attribution first —
    ///         which is all 93 SonarQube ids and all 15 ReSharper inspection names in the catalogue can
    ///         ever be, since those ids only appear at all when that analyzer is in the same build — and
    ///         as best-effort suppression second. Anything that widens this has to argue with this test.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Supersession_DoesNotReachAClaimantOnAnAdjacentLine() {
        var skala = new Finding {
            RuleId = RuleIds.FileScopedNamespace,
            Severity = SkalaSeverity.Info,
            Message = "skala",
            Path = "/a.cs",
            Line = 3,
            Column = 1
        };

        var offByALine = skala with { RuleId = "IDE0161", Message = "roslyn", Line = 4 };
        var offByAColumn = skala with { RuleId = "IDE0161", Message = "roslyn", Column = 2 };

        Assert.All(
            Supersession.Apply([skala, offByALine, offByAColumn]).Where(static f => f.RuleId == "IDE0161"),
            static finding => Assert.Equal(SuppressionKind.None, finding.Suppression)
        );
    }

    /// <summary>
    ///     docs/plan/15 § M5's definition of done, as a test rather than as a claim.
    /// </summary>
    /// <remarks>
    ///     ⚠ The budget is one second on a five-file change with no project loaded, cold, including
    ///     everything the command does. It is asserted with a generous band because a shared CI machine
    ///     is not the reference machine; the measured number is in docs/plan/13.
    /// </remarks>
    [Fact]
    public void Verify_OnAFiveFileChangeWithNoProject_IsUnderASecond() {
        using var scratch = new Scratch();
        for (var i = 0; i < 5; i++) {
            scratch.Write(
                $"File{i}.cs",
                $"namespace Scratch;\n\npublic sealed class File{i} {{\n    public int Value {{ get; set; }}\n}}\n"
            );
        }

        // One warm-up: the first call in a process pays Roslyn's static initialisation and the
        // framework reference read, which the daemon pays once and a hook never pays at all.
        VerifyCommand.Run(
            new VerifyRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], NoCache = true },
            TestContext.Current.CancellationToken
        );

        var stopwatch = Stopwatch.StartNew();
        var result = VerifyCommand.Run(
            new VerifyRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root], NoCache = true },
            TestContext.Current.CancellationToken
        );

        stopwatch.Stop();

        Assert.Equal(ExitCodes.Ok, result.ExitCode);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 1000,
            $"skala verify on a five-file change took {stopwatch.ElapsedMilliseconds} ms; docs/plan/15 § M5's budget is under a second."
        );
    }

    static string Describe(RunReport report) =>
        string.Join(
            "\n",
            report.Reportable
                .Select(static finding => $"{finding.RuleId} {Path.GetFileName(finding.Path)}:{finding.Line}:{finding.Column} {finding.Message}"
                )
                .Order(StringComparer.Ordinal)
        );
}
