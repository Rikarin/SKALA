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

    [Solution(GenerateProjects = false)]
    readonly Solution Solution = null!;

    AbsolutePath SourceDirectory => RootDirectory;

    Target Clean =>
        definition => definition
            .Before(Restore)
            .Executes(() => {
                    SourceDirectory.GlobDirectories("**/bin", "**/obj")
                        .Where(path => !path.ToString().Contains("build", System.StringComparison.Ordinal))
                        .DeleteDirectories();
                }
            );

    Target Restore =>
        definition => definition
            .Executes(() => DotNetRestore(settings => settings.SetProjectFile(Solution)));

    Target Compile =>
        definition => definition
            .DependsOn(Restore)
            .Executes(() => DotNetBuild(settings => settings
                        .SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                        .EnableNoRestore()
                )
            );

    Target Test =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => DotNetTest(settings => settings
                        .SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .EnableNoRestore()
                )
            );

    /// <summary>
    /// ADR-015 — Skala formats Skala, with the configuration gate beside it.
    /// </summary>
    /// <remarks>
    /// ⚠ Testing/corpus is excluded on purpose. Those files are inputs: half of them are
    /// deliberately misformatted and the rest are vendored from other people's trees, and a
    /// formatter that reformats its own test corpus has destroyed its own measurement.
    /// </remarks>
    Target Lint =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    var cli = RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj";
                    DotNetRun(settings => settings
                            .SetProjectFile(cli)
                            .SetConfiguration(Configuration)
                            .EnableNoBuild()
                            .EnableNoRestore()
                            .SetApplicationArguments("config", "check", RootDirectory)
                    );

                    foreach (var area in new[] {
                            "Analysis", "Core", "Formatting", "Reporting", "Rules", "Testing", "Tools"
                        }) {
                        var directory = RootDirectory / area;
                        if (area == "Testing") {
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Testing"));
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Conformance.Tests"));
                            continue;
                        }

                        if (area == "Rules") {
                            // ⚠ Rules/Rikarin.Skala.Rules.Tests/fixtures/ is excluded for the reason
                            // Testing/corpus is: those files are inputs. Half of them are deliberately
                            // written in the shape a rule fires on, and formatting them destroys the
                            // measurement they exist to make.
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Rules"));
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Rules.Generator"));
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Rules.Metadata"));
                            foreach (var file in (directory / "Rikarin.Skala.Rules.Tests").GlobFiles("*.cs")) {
                                DotNetRun(settings => Format(settings, cli, file));
                            }

                            continue;
                        }

                        DotNetRun(settings => Format(settings, cli, directory));
                    }
                }
            );

    DotNetRunSettings Format(DotNetRunSettings settings, AbsolutePath cli, AbsolutePath target) =>
        settings
            .SetProjectFile(cli)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetApplicationArguments("format", "--check", "--quiet", target);

    /// <summary>
    /// The differential suite: the fidelity number, the properties, and the per-option units.
    /// </summary>
    /// <remarks>
    /// It reads the committed fixtures, not JetBrains — the oracle is a developer-machine and
    /// nightly dependency (ADR-011), and `dotnet test` works on a machine with no ReSharper.
    /// </remarks>
    Target Conformance =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => DotNetTest(settings => settings
                        .SetProjectFile(RootDirectory / "Testing" / "Rikarin.Skala.Conformance.Tests")
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .EnableNoRestore()
                )
            );

    /// <summary>
    /// ⚠ Regenerates the committed <c>.expected.cs</c> fixtures from `jb cleanupcode`.
    /// </summary>
    /// <remarks>
    /// A deliberate, reviewed action, and never automatic: an oracle that updates itself when it
    /// disagrees is a tautology (docs/plan/12 § "The oracle"). Its diff is reviewed in its own
    /// commit, whose message says which ReSharper version and why.
    /// </remarks>
    Target Oracle =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => DotNetRun(settings => settings
                        .SetProjectFile(RootDirectory / "Testing" / "Rikarin.Skala.Testing")
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .EnableNoRestore()
                        .SetApplicationArguments("oracle")
                )
            );

    /// <summary>The differential report without a pass/fail: the ranked work queue.</summary>
    /// <remarks>
    /// ⚠ Two reports, because they answer different questions and only the second is the bar.
    /// <c>fidelity</c> ranks the divergence classes by line count, which is what to work on next;
    /// <c>constructs</c> attributes every divergent line to the construct that owns it and puts it
    /// beside how often that construct occurs, which is what docs/plan/16 § R1 actually asks — any
    /// construct occurring more than 50 times must be at 100 %, and a percentage cannot say whether
    /// it is.
    /// </remarks>
    Target Fidelity =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    DotNetRun(settings => Harness(settings, "fidelity"));
                    DotNetRun(settings => Harness(settings, "constructs"));
                }
            );

    DotNetRunSettings Harness(DotNetRunSettings settings, string command) =>
        settings
            .SetProjectFile(RootDirectory / "Testing" / "Rikarin.Skala.Testing")
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetApplicationArguments(command);

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
