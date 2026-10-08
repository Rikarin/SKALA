using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #388 and #384: one project property, <c>GenerateDocumentationFile</c>, decided both what Skala's
///     documentation rules saw and whether the compiler's documentation diagnostics existed at all.
/// </summary>
/// <remarks>
///     ⚠ <b>The regression is the pair, not either half.</b> The same source is checked under both
///     property values, through the binlog — the path CI and the self-gate run — and through the
///     workspace, and the verdict of every rule that reads a <c>///</c> comment must be the same in all
///     four. Before the fix the binlog half with the property off reported both documented members as
///     undocumented (<c>SK7010</c>) and lost the one duplicated comment (<c>SK7100</c>), while the other
///     three agreed with each other.
///     <para>
///         ⚠ The undocumented member is the anti-vacuity control. Without it, "no <c>SK7010</c>" is
///         satisfied by a rule that never ran, which is the confusion #388 is about; with it, the rule
///         has to have run and to have told the two members apart.
///     </para>
///     <para>
///         ⚠ Sabotage, run: making <c>DocumentationComments.ForAnalysis</c> return its input unchanged
///         turns the binlog/off row red — every documented public member comes back as <c>SK7010</c>
///         and the <c>SK7100</c> disappears. The workspace rows stay green under that sabotage,
///         because <c>MSBuildWorkspace</c> already hands back <see cref="DocumentationMode.Parse" /> for
///         a project without the property, which is why the defect was binlog-only. Dropping the note
///         from <c>ProjectLoader</c> turns both property-off rows of the #384 theory red.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class DocumentationModeTests {
    // ⚠ Named for the concept: `ToolDiagnosticIdTests` lets one id be declared twice only as a mirror.
    const string PublicApiCommentDensity = "SK7010";
    const string NonPublicMemberNotDocumented = "SK7101";
    const string DocumentationDuplicatesBaseMember = "SK7100";
    const string DocumentationDiagnosticsOff = "SK9032";

    const string Source = """
                          namespace Probe;

                          /// <summary>A documented type.</summary>
                          public sealed class Doc {
                              /// <summary>A documented method.</summary>
                              public void Run() { }

                              /// <summary>Renamed parameter.</summary>
                              /// <param name="old">The old name.</param>
                              public void Renamed(int current) { }

                              public void Undocumented() { }

                              /// <summary>A documented internal.</summary>
                              internal void Inside() { }
                          }

                          /// <summary>A base.</summary>
                          public abstract class Store {
                              /// <summary>Removes every entry.</summary>
                              public abstract void Clear();
                          }

                          /// <summary>A derived store.</summary>
                          public sealed class MemoryStore : Store {
                              /// <summary>Removes every entry.</summary>
                              public override void Clear() { }
                          }
                          """;

    /// <summary>
    ///     Every documentation rule's verdict, as <c>rule@line</c>, so four runs compare as four sets.
    /// </summary>
    static readonly string[] Expected = [
        PublicApiCommentDensity + "@12", // `Undocumented`, and nothing else
        DocumentationDuplicatesBaseMember + "@26" // `MemoryStore.Clear`'s copied comment
    ];

    [Theory]
    [InlineData(LoadMode.Binlog, false)]
    [InlineData(LoadMode.Binlog, true)]
    [InlineData(LoadMode.Workspace, false)]
    [InlineData(LoadMode.Workspace, true)]
    public void DocumentationRules_GiveOneVerdict_WhateverTheBuildAskedFor(LoadMode mode, bool generate) {
        using var scratch = new Scratch();
        var report = Check(scratch, mode, generate);

        var verdict = report.Reportable
            .Where(static finding => finding.RuleId is PublicApiCommentDensity
                    or NonPublicMemberNotDocumented
                    or DocumentationDuplicatesBaseMember
            )
            .Select(static finding => finding.RuleId
                + "@"
                + finding.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Expected, verdict);
    }

    /// <summary>
    ///     #384: the compiler's documentation diagnostics exist only when the build asked for them, and
    ///     a run where they did not is said to be one — once, at info, without moving the exit code.
    /// </summary>
    [Theory]
    [InlineData(LoadMode.Binlog, false)]
    [InlineData(LoadMode.Binlog, true)]
    [InlineData(LoadMode.Workspace, false)]
    [InlineData(LoadMode.Workspace, true)]
    public void CompilerDocumentationDiagnostics_AreEitherReportedOrSaidToBeOff(LoadMode mode, bool generate) {
        using var scratch = new Scratch();
        var report = Check(scratch, mode, generate);

        var compiler = report.Reportable.Where(static finding => finding.RuleId is "CS1572" or "CS1573").ToList();
        var notes = report.Diagnostics.Where(static d => d.Id == DocumentationDiagnosticsOff).ToList();

        if (generate) {
            Assert.Equal(2, compiler.Count);
            Assert.Empty(notes);
        } else {
            // ⚠ Parse, never Diagnose: normalising the tree must not conjure the family back up.
            Assert.Empty(compiler);
            var note = Assert.Single(notes);
            Assert.Equal(SkalaSeverity.Info, note.Severity);
            Assert.Contains("Probe.csproj", note.Message, StringComparison.Ordinal);
            Assert.False(Gate.FailsReliability(note));
        }
    }

    /// <summary>
    ///     The note names a project once however many compilations it has, and says nothing when every
    ///     project asked for documentation.
    /// </summary>
    [Fact]
    public void Note_NamesEachProjectOnce_AndIsAbsentWhenNothingIsOff() {
        var root = Path.GetTempPath();
        var a = Path.Combine(root, "A", "A.csproj");
        var b = Path.Combine(root, "B", "B.csproj");

        var units = new[] { Unit("A(net8.0)", a, true), Unit("A(net10.0)", a, true), Unit("B", b, false) };

        var note = Assert.Single(DocumentationComments.Note([..units], root));
        Assert.Equal(DocumentationDiagnosticsOff, note.Id);
        Assert.StartsWith(
            "1 of 2 projects do not set GenerateDocumentationFile",
            note.Message,
            StringComparison.Ordinal
        );
        Assert.EndsWith(Path.Combine("A", "A.csproj"), note.Message, StringComparison.Ordinal);

        Assert.Empty(DocumentationComments.Note([Unit("B", b, false)], root));

        // Past three the message counts the rest and the detail names every one.
        var many = Enumerable.Range(0, 5)
            .Select(index => Unit("P" + index, Path.Combine(root, "P" + index, "P.csproj"), true))
            .ToArray();
        var crowded = Assert.Single(DocumentationComments.Note([..many], root));
        Assert.EndsWith(" and 2 more", crowded.Message, StringComparison.Ordinal);
        Assert.Equal(5, crowded.Detail!.Split(", ").Length);
    }

    /// <summary>Only the documentation mode moves; every other parse option is the build's.</summary>
    [Fact]
    public void ForAnalysis_ChangesOnlyTheDocumentationMode() {
        var build = new CSharpParseOptions(LanguageVersion.CSharp10, DocumentationMode.None)
            .WithPreprocessorSymbols("RELEASE", "SKALA_PROBE");

        var analysed = DocumentationComments.ForAnalysis(build);

        Assert.Equal(DocumentationMode.Parse, analysed.DocumentationMode);
        Assert.Equal(LanguageVersion.CSharp10, analysed.LanguageVersion);
        Assert.Equal(build.PreprocessorSymbolNames, analysed.PreprocessorSymbolNames);

        var diagnose = build.WithDocumentationMode(DocumentationMode.Diagnose);
        Assert.Same(diagnose, DocumentationComments.ForAnalysis(diagnose));
    }

    /// <summary>
    ///     ⚠ The second casualty of <see cref="DocumentationMode.None" />, and the quieter one: the
    ///     compiler reports no unnecessary using (<c>CS8019</c>) at all in a tree parsed without
    ///     documentation, because it cannot know whether a <c>cref</c> needed the import.
    /// </summary>
    /// <remarks>
    ///     <c>UsingsRule.Unused</c> reads exactly that diagnostic, so before #388 an
    ///     <c>arrange</c> over a binlog of a project without <c>GenerateDocumentationFile</c> could never
    ///     remove an unused using — a zero from an instrument that did not run, on the CI path — while
    ///     the workspace load of the same project removed them.
    /// </remarks>
    [Theory]
    [InlineData(DocumentationMode.None, 0)]
    [InlineData(DocumentationMode.Parse, 1)]
    public void UnnecessaryUsing_IsReportedOnlyWhereDocumentationIsParsed(DocumentationMode mode, int expected) {
        var tree = CSharpSyntaxTree.ParseText(
            "using System.Text;\n\nnamespace Probe;\n\npublic static class C { }\n",
            new(LanguageVersion.Preview, mode),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "Probe",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new(OutputKind.DynamicallyLinkedLibrary)
        );

        var unnecessary = compilation.GetSemanticModel(tree)
            .GetDiagnostics(cancellationToken: TestContext.Current.CancellationToken)
            .Count(static diagnostic => diagnostic.Id == "CS8019");

        Assert.Equal(expected, unnecessary);
    }

    static CompilationUnit Unit(string name, string project, bool off) =>
        new() {
            Name = name,
            Compilation = CSharpCompilation.Create(name),
            ProjectPath = project,
            DocumentationDiagnosticsOff = off
        };

    static RunReport Check(Scratch scratch, LoadMode mode, bool generate) {
        var project = scratch.Write(
            "Probe.csproj",
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net10.0</TargetFramework>
                 <GenerateDocumentationFile>{(generate ? "true" : "false")}</GenerateDocumentationFile>
               </PropertyGroup>
             </Project>
             """
        );

        // ⚠ Cut both inheritance chains: an inherited property would make the two halves equal, and an
        // inherited `.editorconfig` could leave the two opt-in rules at `none`, where a zero is vacuous.
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        scratch.Write(
            ".editorconfig",
            """
            root = true

            [*.cs]
            dotnet_diagnostic.SK7010.severity = warning
            dotnet_diagnostic.SK7101.severity = warning
            dotnet_diagnostic.SK7100.severity = warning
            """
        );
        scratch.Write("Doc.cs", Source);

        string? binlog = null;
        if (mode == LoadMode.Binlog) {
            binlog = Path.Combine(scratch.Root, "probe.binlog");
            Build(project, binlog);
        }

        var (exit, report) = CheckCommand.Run(
            new() {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = mode,
                BinlogPath = binlog,
                ProjectPath = project,
                AllowLoadFallback = false,
                Output = string.Empty,
                IncludeFormatting = false,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        // The note is info and the documentation findings are warnings under the `local` gate, which
        // fails on neither: the run's exit is the same whichever half of the pair it is.
        Assert.Equal(ExitCodes.Ok, exit.ExitCode);
        Assert.Equal(mode, report.Mode);

        return report;
    }

    /// <summary>⚠ A binlog is the record of a real build, so one has to be run to get a real one.</summary>
    static void Build(string project, string binlog) {
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        foreach (var argument in new[] { "build", project, "-bl:" + binlog, "--nologo" }) {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "`dotnet build` on the documentation fixture failed:\n" + output);
    }
}
