using System.Reflection;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     <c>--load=workspace</c>: that it runs at all, and that it fails closed when it cannot.
/// </summary>
/// <remarks>
///     ⚠ <b>The mode had no test of any kind, and it had never run.</b>
///     <c>Microsoft.CodeAnalysis.Workspaces.MSBuild</c> was referenced with
///     <c>ExcludeAssets="runtime"</c> — correct for the four <c>Microsoft.Build.*</c> packages beside
///     it, which MSBuildLocator insists on resolving from the SDK, and wrong for this one, which is
///     Roslyn's and which the SDK does not ship. So <c>MSBuildWorkspace.Create()</c> threw
///     <c>FileNotFoundException</c> on every invocation the tool has ever shipped, and
///     <c>LoadingTests</c> exercised only the loose path, so nothing noticed for five milestones.
///     <para>
///         ⚠ The two halves are tested separately on purpose, because fixing the reference alone
///         leaves the worse half standing. The load failure did not stop the run: the ladder fell
///         through to the syntactic loader, the syntactic rules found nothing to gate on, and the gate
///         reported a clean tree. Both directions are asserted here — that a good project produces
///         findings, and that a project which cannot be loaded produces a non-zero exit.
///     </para>
/// </remarks>
public sealed class WorkspaceLoadingTests {
    const string Unformatted = """
                               using System;

                               namespace Scratch;

                               public class Widget
                               {
                                   public   int  Value {get;set;}

                                   public void Do( ) {
                                     var unused = 5;
                                     Console.WriteLine( "x" ) ;
                                   }
                               }
                               """;

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                               <Nullable>enable</Nullable>
                             </PropertyGroup>
                           </Project>
                           """;

    /// <summary>
    ///     ⚠ A .csproj naming an SDK that does not exist. <c>MSBuildWorkspace</c> does not throw on
    ///     this: it records a <c>WorkspaceDiagnosticKind.Failure</c> and hands back a placeholder
    ///     project with no documents, which is why the fail-closed test below cannot be written
    ///     against the project count.
    /// </summary>
    const string UnloadableProject = """
                                     <Project Sdk="Definitely.Not.A.Real.Sdk">
                                       <PropertyGroup>
                                         <TargetFramework>net10.0</TargetFramework>
                                       </PropertyGroup>
                                     </Project>
                                     """;

    /// <summary>
    ///     ⚠ The cheapest guard, and the one that fails first if the reference regresses.
    /// </summary>
    /// <remarks>
    ///     Everything below it needs an SDK on the machine and a design-time build; this needs
    ///     neither, so a packaging regression is a one-line failure naming the assembly rather than a
    ///     timeout or an unrelated-looking load error somewhere in Roslyn.
    /// </remarks>
    [Fact]
    public void TheWorkspaceAssemblyShipsBesideTheTool() {
        var exception = Record.Exception(static () => Assembly.Load("Microsoft.CodeAnalysis.Workspaces.MSBuild"));

        Assert.True(
            exception is null,
            "Microsoft.CodeAnalysis.Workspaces.MSBuild is not loadable at run time. It is Roslyn's "
            + "assembly, not MSBuild's, and the SDK does not supply it: it must not carry "
            + "ExcludeAssets=runtime in Analysis/Rikarin.Skala.Analysis/Rikarin.Skala.Analysis.csproj. "
            + $"Load failed with: {exception?.Message}"
        );
    }

    /// <summary>
    ///     ⚠ Asserts on what came back, not on an exit code. A gate that returns nothing is the bug, so
    ///     "exit 0" is exactly the answer the defect produced and cannot be the thing under test.
    /// </summary>
    /// <remarks>
    ///     <c>CS0219</c> is the load-bearing assertion: an unused-local warning is the compiler's, so
    ///     it can only appear if a real compilation with real references was built. The loose loader
    ///     would supply <c>SK0001</c> for the same file and prove nothing, which is precisely how the
    ///     silent fallback stayed invisible.
    /// </remarks>
    [Fact]
    public void Workspace_LoadsARealProjectAndProducesFindings() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);
        var project = scratch.Write("Scratch.csproj", Project);

        var (result, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Output = string.Empty,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Workspace, report.Mode);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.NotEmpty(report.Findings);
        Assert.Contains(report.Findings, static finding => finding.RuleId == "CS0219");
    }

    /// <summary>
    ///     ⚠ The other direction, and the half a reference fix does not cover: a workspace that will
    ///     not load must be a load failure and not a pass.
    /// </summary>
    /// <remarks>
    ///     Before this, the run went workspace → nothing analysable → loose → the syntactic rules over
    ///     the same files → exit 0. Nothing in the report said the project had failed to evaluate
    ///     except a warning nobody read, and under <c>SkalaMode=check</c> that is a green CI build over
    ///     an unanalysed tree.
    /// </remarks>
    [Fact]
    public void Workspace_ThatCannotBeLoadedIsALoadFailureRatherThanAPass() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);
        var project = scratch.Write("Scratch.csproj", UnloadableProject);

        var (result, _) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Output = string.Empty,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(ExitCodes.Ok, result.ExitCode);
        Assert.Equal(ExitCodes.LoadFailure, result.ExitCode);
    }

    /// <summary>
    ///     ⚠ And the loader's own view of it, so the reason survives a change of command.
    /// </summary>
    [Fact]
    public void Workspace_ThatCannotBeLoadedDoesNotFallThroughToLoose() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);
        var project = scratch.Write("Scratch.csproj", UnloadableProject);

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Workspace, ProjectPath = project },
            TestContext.Current.CancellationToken
        );

        Assert.True(loaded.Failed);
        Assert.Equal(LoadMode.Workspace, loaded.Mode);
        Assert.Contains(loaded.Diagnostics, static d => d.Severity >= SkalaSeverity.Error);
    }

    /// <summary>
    ///     ⚠ The guard on the fix, and the reason <c>Failed</c> is scoped to the mode the caller named.
    /// </summary>
    /// <remarks>
    ///     Workspace is also the *middle* rung of the default binlog ladder. Refusing to fall through
    ///     to loose there would turn "this machine cannot evaluate the project" into "the tool will not
    ///     run", which is a worse failure than the one being fixed and would hit every default
    ///     <c>skala check</c> on a repository with no binlog.
    /// </remarks>
    [Fact]
    public void Binlog_StillFallsThroughAFailedWorkspaceToLoose() {
        using var scratch = new Scratch();
        scratch.Write("Widget.cs", Unformatted);
        scratch.Write("Scratch.csproj", UnloadableProject);

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Binlog },
            TestContext.Current.CancellationToken
        );

        Assert.False(loaded.Failed);
        Assert.Equal(LoadMode.Loose, loaded.Mode);
        Assert.NotEmpty(loaded.Units);
    }
}
