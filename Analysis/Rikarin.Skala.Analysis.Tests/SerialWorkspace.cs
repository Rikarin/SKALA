namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     The one collection every test that opens an <c>MSBuildWorkspace</c> belongs to, so that no two
///     of them run at once.
/// </summary>
/// <remarks>
///     ⚠ <b>Not tidiness — the Windows runner fails without it.</b> Roslyn's
///     <c>MSBuildWorkspace</c> starts an out-of-process build host and talks to it over a named pipe.
///     xUnit runs test classes in parallel, eight classes here open a workspace, and on a two-core
///     Windows runner the concurrent hosts time out connecting:
///     <code>
///     System.Exception : The build host was started but we were unable to connect to it's pipe.
///     The process exited with -1. Process output: info: BuildHost Runtime Version: .NET 10.0.11
///     ---- System.TimeoutException : The operation has timed out.
///     </code>
///     The host process *starts* — it prints its version — and the connection is what times out. The
///     failure moved between classes run to run (one test at <c>05e05a11</c>, a different one at
///     <c>cc294436</c>, three at <c>5376a22a</c>), which is what a contention problem looks like and
///     what a broken test does not.
///     <para>
///         ⚠ <b>An earlier diagnosis blamed NUKE and is refuted.</b> The release workflow's
///         <c>./build.sh Test</c> was changed to <c>dotnet build</c> + <c>dotnet test</c> because
///         cross-platform.yml, which already ran those two commands, was green on the same commit.
///         It was green by luck: cross-platform's Windows leg has since failed the same way, with the
///         same message, running the invocation that was supposed to be the fix. The common factor is
///         the runner and the parallelism, not the build tool.
///     </para>
///     <para>
///         Linux and macOS runners have not shown it. They are not immune — they are faster to start a
///         process — so the collection applies everywhere rather than under an OS condition.
///     </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialWorkspace {
    // ⚠ `internal`, because `SK6034` is right about it: a `public const` is copied into every caller
    // at compile time. Nothing outside this assembly names a collection defined in it.
    internal const string Name = "MSBuild workspace";
}
