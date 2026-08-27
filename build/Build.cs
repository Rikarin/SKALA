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
            Skala("config", "check", RootDirectory);
        });

    /// <summary>
    /// Regenerate the distributable canonical payload from the Rider export.
    /// </summary>
    /// <remarks>
    /// ⚠ ADR-001's maintainer loop, and the only step in it that is not "use Rider": change a
    /// setting in Rider, re-export over <c>editor_config_template</c>, run this, commit, publish.
    /// <c>CanonicalDistributionTests</c> fails when the checked-in payload is not what this target
    /// would produce, so a re-export that skips this step is a red build rather than a silent
    /// divergence between the export and what eighteen repositories are given.
    /// </remarks>
    Target Canonical => definition => definition
        .DependsOn(Compile)
        .Executes(() => {
            Skala(
                "config", "canonical",
                RootDirectory / "editor_config_template",
                "--out", CanonicalDirectory,
                "--version", CanonicalVersion);
        });

    /// <summary>The published artefacts. `Rikarin.Skala.Canonical` is the only one packable today.</summary>
    Target Pack => definition => definition
        .DependsOn(Compile)
        .Executes(() => DotNetPack(settings => settings
            .SetProject(CanonicalDirectory / "Rikarin.Skala.Canonical.csproj")
            .SetConfiguration(Configuration)
            .SetOutputDirectory(RootDirectory / "artifacts" / "packages")
            .EnableNoBuild()
            .EnableNoRestore()));

    AbsolutePath CanonicalDirectory => RootDirectory / "Distribution" / "Rikarin.Skala.Canonical";

    /// <summary>
    /// The canonical's version, which is deliberately not the tool's: a canonical bump is a
    /// repository-wide reformatting commit and a tool bump is not, and tying them together forces
    /// every repository to take the reformat to get a bug fix.
    /// </summary>
    [Parameter("The version stamped into the canonical manifest")]
    readonly string CanonicalVersion = "0.1.0";

    void Skala(params object[] arguments) {
        var cli = RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj";
        DotNetRun(settings => settings
            .SetProjectFile(cli)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetApplicationArguments(arguments.Select(static argument => argument.ToString()!).ToArray()));
    }
}
