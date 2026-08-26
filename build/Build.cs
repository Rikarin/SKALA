using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>
/// `./build.sh Compile`, `./build.sh Test`, `./build.sh Lint`.
/// </summary>
/// <remarks>
/// docs/plan/11-cli-and-integrations.md: the build is NUKE because Vixen's is, and one build
/// system across the author's repositories is worth more than the best one in each.
/// </remarks>
class Build : NukeBuild {
    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration — Debug locally, Release in CI")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = false)] readonly Solution Solution = null!;

    AbsolutePath SourceDirectory => RootDirectory;

    Target Clean => definition => definition
        .Before(Restore)
        .Executes(() => {
            SourceDirectory.GlobDirectories("**/bin", "**/obj")
                .Where(path => !path.ToString().Contains("build", System.StringComparison.Ordinal))
                .DeleteDirectories();
        });

    Target Restore => definition => definition
        .Executes(() => DotNetRestore(settings => settings.SetProjectFile(Solution)));

    Target Compile => definition => definition
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(settings => settings
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    Target Test => definition => definition
        .DependsOn(Compile)
        .Executes(() => DotNetTest(settings => settings
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()));

    /// <summary>
    /// ADR-015 — Skala formats Skala. Until the formatter exists (Milestone 1) the lint gate is the
    /// configuration gate: the repository's own .editorconfig must have no configuration errors.
    /// </summary>
    Target Lint => definition => definition
        .DependsOn(Compile)
        .Executes(() => {
            var cli = RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj";
            DotNetRun(settings => settings
                .SetProjectFile(cli)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetApplicationArguments("config", "check", RootDirectory));
        });
}
