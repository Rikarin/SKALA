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

    [Parameter("Runtime identifier for `Native` — defaults to the host's")]
    readonly string Runtime = null!;

    /// <summary>
    /// The shipping layout: a NativeAOT <c>skala</c> beside a ReadyToRun <c>skala-tool</c>.
    /// </summary>
    /// <remarks>
    /// docs/plan/13 § "Startup". The two halves have to be published together and land in one
    /// directory, because that adjacency is how the client finds the tool — see
    /// <c>Fallback.Locate</c>, which deliberately looks beside its own executable *before* it looks
    /// at <c>SKALA_TOOL</c> or the path. Two Skala versions formatting one repository is the failure
    /// doc 11 § "Distribution"'s version pinning exists to prevent, and picking up whichever
    /// `skala-tool` happens to be on the PATH is exactly how it happens.
    /// <para>
    /// ⚠ Measured on the reference machine (M-series, 10 cores), 200 runs in a shell loop so that
    /// the harness is not the measurement: bare process start is <b>1.68 ms</b> for
    /// <c>/usr/bin/true</c>, <b>4.85 ms</b> for the AOT client, and <b>79.5 ms</b> for the framework
    /// dependent tool. The client is the difference between meeting the 40 ms warm budget and
    /// spending twice it before <c>Main</c> runs.
    /// </para>
    /// </remarks>
    Target Native =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    var rid = Runtime ?? System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

                    var output = RootDirectory / "artifacts" / "native" / rid;
                    output.CreateOrCleanDirectory();

                    // The full tool first: the client is useless without something to fall back to.
                    DotNetPublish(settings => settings
                        .SetProject(RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj")
                        .SetConfiguration(Configuration)
                        .SetRuntime(rid)
                        .SetSelfContained(false)
                        .SetOutput(output)
                    );

                    DotNetPublish(settings => settings
                        .SetProject(RootDirectory / "Tools" / "Rikarin.Skala.Client" / "Rikarin.Skala.Client.csproj")
                        .SetConfiguration(Configuration)
                        .SetRuntime(rid)
                        .SetOutput(output)
                    );

                    Serilog.Log.Information("Native layout in {Output}", output);
                }
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

    /// <summary>
    /// Regenerate every documentation surface the two registries define.
    /// </summary>
    /// <remarks>
    /// `docs/rules/*.md` and `docs/site/` are both committed and both generated, from
    /// `Rules/Rikarin.Skala.Rules.Metadata/rules.json` and
    /// `Core/Rikarin.Skala.Options/options.json` (docs/plan/08 § "Documentation", docs/plan/15 § M7).
    /// One target rather than two, because the failure this exists to prevent is regenerating one of
    /// them and forgetting the other, and `RuleCatalogTests.DocsPages_AreUpToDate` and
    /// `DocsSiteTests.Site_IsUpToDateWithTheSources` then fail one at a time in separate assemblies.
    /// <para>
    /// ⚠ Deliberately not part of `Compile` or `Lint`. A build step that rewrites tracked files
    /// turns `dotnet build` into something that dirties the worktree, and the two tests already make
    /// a forgotten regeneration a red build — which is the mechanism. This is how you satisfy them.
    /// </para>
    /// </remarks>
    Target Docs => definition => definition
        .DependsOn(Compile)
        .Executes(() => {
            Skala("rules", "docs", RootDirectory / "docs" / "rules");
            Skala("docs", "site", RootDirectory / "docs" / "site");
        });

    /// <summary>
    /// The five published artefacts of docs/plan/02 § "Package boundaries".
    /// </summary>
    /// <remarks>
    /// `Rikarin.Skala.Rules`, `Rikarin.Skala.Canonical`, `Rikarin.Skala.MSBuild` and
    /// `Rikarin.Skala.Sdk` are ordinary packs. `Rikarin.Skala.Cli` is not, and the shape of this
    /// target is that difference.
    /// <para>
    /// ⚠ The tool package is <b>RID-specific</b>, because its command is a NativeAOT binary. .NET 10
    /// packs that as <c>tools/any/&lt;rid&gt;/</c> with <c>Runner="executable"</c> in
    /// <c>DotnetToolSettings.xml</c>; <c>Runner="dotnet"</c> — the only option before — can only
    /// name a managed assembly, which would put the 79.5 ms framework-dependent tool back on the
    /// hook path for everyone who installs from NuGet.
    /// </para>
    /// <para>
    /// ⚠ The two publishes happen <b>here</b> and in this order, rather than inside the .csproj:
    /// the full tool first, because the client is useless without something to fall back to, and
    /// both into one staging directory, because that adjacency is how <c>Fallback.Locate</c> finds
    /// the tool. A nested publish inside pack is a second evaluation of the project graph in the
    /// middle of the first; pack is then only a copy.
    /// </para>
    /// <para>
    /// ⚠ Packing more than one RID also produces a RID-agnostic wrapper package of the same id whose
    /// <c>DotnetToolSettings.xml</c> lists the per-RID package ids. Publishing the wrapper without
    /// every package it names produces an install that fails on the platform whose package is
    /// missing, so the default is the host RID alone and the full matrix is an explicit
    /// <c>--rids</c>.
    /// </para>
    /// </remarks>
    Target Pack => definition => definition
        .DependsOn(Compile)
        .Executes(() => {
            var packages = RootDirectory / "artifacts" / "packages";
            packages.CreateOrCleanDirectory();

            // ⚠ Two properties, and the second one is the difference between a package that
            // installs and one that cannot.
            //
            // NU5128 — "no lib/ or ref/ for the framework in the dependency group" — is what an
            // analyzer package *is*: the assembly ships under analyzers/dotnet/cs and nothing goes
            // in lib/. It is a warning, TreatWarningsAsErrors makes it an error, and the two
            // content-only packages suppress it in their own .csproj.
            //
            // ⚠ SuppressDependenciesWhenPacking, because `Rikarin.Skala.Rules` has a ProjectReference
            // to `Rikarin.Skala.Rules.Metadata` and the reference becomes a .nuspec dependency on a
            // package id **that is not published** — doc 02's table has five packages and that is
            // not one of them. Measured in a fresh repository against a local feed:
            //
            //   error NU1101: Unable to find package Rikarin.Skala.Rules.Metadata.
            //     No packages exist with this id in source(s): …, local-skala, nuget.org
            //
            // The analyzer package has been unrestorable by anybody since it was written, and
            // nothing said so because nothing had ever installed it. The dependency is also
            // redundant: `Rules.csproj` already packs `Rikarin.Skala.Rules.Metadata.dll` into
            // `analyzers/dotnet/cs` beside its own, which is where Roslyn looks.
            //
            // Both are set here rather than in the .csproj because Rules/ is a rules concern and
            // this is a packaging one.
            DotNetPack(settings => settings
                .SetProject(RootDirectory / "Rules" / "Rikarin.Skala.Rules" / "Rikarin.Skala.Rules.csproj")
                .SetConfiguration(Configuration)
                .SetOutputDirectory(packages)
                .SetProperty("NoWarn", "NU5128")
                .SetProperty("SuppressDependenciesWhenPacking", "true")
                .EnableNoBuild()
                .EnableNoRestore());

            foreach (var project in new[] {
                    CanonicalDirectory / "Rikarin.Skala.Canonical.csproj",
                    RootDirectory / "Tools" / "Rikarin.Skala.MSBuild" / "Rikarin.Skala.MSBuild.csproj",
                    RootDirectory / "Distribution" / "Rikarin.Skala.Sdk" / "Rikarin.Skala.Sdk.csproj"
                }) {
                DotNetPack(settings => settings
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(packages)
                    .EnableNoBuild()
                    .EnableNoRestore());
            }

            foreach (var rid in ToolRuntimes) {
                var payload = RootDirectory / "artifacts" / "tool-payload" / rid;
                payload.CreateOrCleanDirectory();

                DotNetPublish(settings => settings
                    .SetProject(RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj")
                    .SetConfiguration(Configuration)
                    .SetRuntime(rid)
                    .SetSelfContained(false)
                    .SetOutput(payload));

                DotNetPack(settings => settings
                    .SetProject(RootDirectory / "Tools" / "Rikarin.Skala.Client" / "Rikarin.Skala.Client.csproj")
                    .SetConfiguration(Configuration)
                    .SetRuntime(rid)
                    .SetOutputDirectory(packages)
                    .SetProperty("IsPackable", "true")
                    .SetProperty("SkalaToolPayload", payload));
            }

            foreach (var package in packages.GlobFiles("*.nupkg").OrderBy(static path => path.Name)) {
                Serilog.Log.Information(
                    "{Package} — {Size:N0} bytes",
                    package.Name,
                    new System.IO.FileInfo(package).Length);
            }
        });

    /// <summary>
    /// The RIDs the tool package is built for. The host's alone by default — see <see cref="Pack"/>
    /// for why a wrapper without all of its per-RID packages is worse than none.
    /// </summary>
    [Parameter("Semicolon-separated RIDs for the tool package — defaults to the host's")]
    readonly string Rids = null!;

    string[] ToolRuntimes =>
        Rids?.Split(';', System.StringSplitOptions.RemoveEmptyEntries)
        ?? [System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier];

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
