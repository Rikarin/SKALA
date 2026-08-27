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
                            .SetProject(
                                RootDirectory / "Tools" / "Rikarin.Skala.Client" / "Rikarin.Skala.Client.csproj"
                            )
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

                    // ⚠ `Distribution` was missing from this list and its two projects' sources
                    // were never checked. There is not much there — a marker type each — but "the
                    // formatter formats its own repository" (ADR-015) is a claim about the
                    // repository and not about six of its seven top-level directories.
                    //
                    // ⚠ `build` is the same hole, found the same way and one directory later: this
                    // file and `Configuration.cs` were never format-checked by the target they
                    // define, and `Build.cs` had drifted out of formatting by the time M10 looked.
                    // It also covers `build/Rikarin.Skala.Release`, the measured-version tool
                    // (docs/plan/18), which would otherwise have arrived unchecked.
                    foreach (var area in new[] {
                                 "Analysis", "build", "Core", "Distribution", "Formatting", "Reporting", "Rules",
                                 "Testing", "Tools"
                             }) {
                        var directory = RootDirectory / area;
                        if (area == "Testing") {
                            // ⚠ Named one by one because Testing/corpus is excluded, and a new
                            // project under Testing/ is therefore invisible to this target until
                            // someone adds it here. That is exactly how Distribution's two projects
                            // went unchecked until M8 (7c56c8f); the sweep is the third entry.
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Testing"));
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Conformance.Tests"));
                            DotNetRun(settings => Format(settings, cli, directory / "Rikarin.Skala.Conformance.Sweep"));
                            DotNetRun(settings => Format(
                                    settings,
                                    cli,
                                    directory / "Rikarin.Skala.Conformance.Sweep.Tests"
                                )
                            );
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

    /// <summary>
    /// ⚠ The key-flip conformance sweep: every option, at every legal value, against the oracle.
    /// </summary>
    /// <remarks>
    /// A nightly job and never a commit gate. It needs JetBrains installed and takes minutes, so
    /// like <see cref="Oracle"/> it is a developer-machine and nightly dependency (ADR-011) and what
    /// the fast path reads is the committed result table. ⚠ Its verdict is three-way and only one
    /// third of it is green: an option whose fixture cannot tell its values apart is reported
    /// <c>UNEXERCISED</c>, which is not a pass — see docs/plan/12 § "The key-flip sweep".
    /// </remarks>
    Target Sweep =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => DotNetRun(settings => settings
                        .SetProjectFile(RootDirectory / "Testing" / "Rikarin.Skala.Conformance.Sweep")
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .EnableNoRestore()
                        .SetApplicationArguments("sweep")
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
    Target Canonical =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    Skala(
                        "config",
                        "canonical",
                        RootDirectory / "editor_config_template",
                        "--out",
                        CanonicalDirectory,
                        "--version",
                        CanonicalVersion
                    );
                }
            );

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
    Target Docs =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    Skala("rules", "docs", RootDirectory / "docs" / "rules");
                    Skala("docs", "site", RootDirectory / "docs" / "site");
                }
            );

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
    Target Pack =>
        definition => definition
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
                            .EnableNoRestore()
                    );

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
                                .EnableNoRestore()
                        );
                    }

                    foreach (var rid in ToolRuntimes) {
                        var payload = RootDirectory / "artifacts" / "tool-payload" / rid;
                        payload.CreateOrCleanDirectory();

                        DotNetPublish(settings => settings
                                .SetProject(RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj")
                                .SetConfiguration(Configuration)
                                .SetRuntime(rid)
                                .SetSelfContained(false)
                                .SetOutput(payload)
                        );

                        DotNetPack(settings => settings
                                .SetProject(
                                    RootDirectory / "Tools" / "Rikarin.Skala.Client" / "Rikarin.Skala.Client.csproj"
                                )
                                .SetConfiguration(Configuration)
                                .SetRuntime(rid)
                                .SetOutputDirectory(packages)
                                .SetProperty("IsPackable", "true")
                                .SetProperty("SkalaToolPayload", payload)
                        );
                    }

                    foreach (var package in packages.GlobFiles("*.nupkg").OrderBy(static path => path.Name)) {
                        Serilog.Log.Information(
                            "{Package} — {Size:N0} bytes",
                            package.Name,
                            new System.IO.FileInfo(package).Length
                        );
                    }
                }
            );

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

    // ── The measured release, docs/plan/18 ────────────────────────────────────────────────────

    [Parameter("The git ref of the release to measure against. Default: the highest `v*` tag.")]
    readonly string BaselineRef = null!;

    [Parameter("The version that ref published. Default: the tag with its `v` removed.")]
    readonly string BaselineVersion = null!;

    /// <summary>
    /// A checkout of the previous release that somebody else has already built.
    /// </summary>
    /// <remarks>
    /// ⚠ Supply this with <see cref="BaselineVersion"/> instead of <see cref="BaselineRef"/> when
    /// the baseline is built outside this build — which is what the workflow does, and what
    /// docs/plan/18 § "Running it" recommends. <c>Materialise</c>'s convenience path builds the
    /// baseline from inside this process and is the one part of the pipeline that is not reliable
    /// everywhere; see its remarks.
    /// </remarks>
    [Parameter("A directory holding the previous release, already built")]
    readonly string BaselineDirectory = null!;

    [Parameter("The previous release's built skala-tool.dll")]
    readonly string BaselineToolPath = null!;

    /// <summary>
    /// Commits since the baseline tag, for the pre-release counter. Derived when not given.
    /// </summary>
    [Parameter("Commits since the baseline tag; the pre-release counter")]
    readonly string Height = null!;

    /// <summary>
    /// Cut a release rather than measure a <c>master</c> build: no <c>-alpha.N</c> on the number.
    /// </summary>
    [Parameter("Cut a release rather than measure a master build")]
    readonly bool Release;

    /// <summary>Where the three release outputs land: the notes, the changelog block, version.json.</summary>
    AbsolutePath ReleaseDirectory => RootDirectory / "artifacts" / "release";

    /// <summary>
    /// The scratch the measurement needs, and it is <b>outside the repository</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ It was inside, under <c>artifacts/release/</c>, and that broke three
    /// <c>ProjectGraphTests</c>: the baseline is a whole second checkout, so <c>ProjectFile.LoadAll</c>
    /// found two <c>Rikarin.Skala.Testing</c>, two <c>Rikarin.Skala.Formatting</c> and two
    /// <c>Rikarin.Skala.Core</c>, and every <c>Assert.Single</c> in that class failed at once. A copy
    /// of the repository inside the repository is a trap for every tree-walking tool this project has
    /// — the graph tests, `skala config check`, `rules docs`, the docs-site check — and the fix is not
    /// to teach each of them a new exclusion.
    /// <para>
    /// Keyed by the root's path so that the several agent worktrees this repository is usually
    /// carrying do not share one scratch directory and measure each other's baselines.
    /// </para>
    /// </remarks>
    AbsolutePath ReleaseScratch =>
        (AbsolutePath)System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "skala-release",
            System.Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(RootDirectory.ToString())
                )
            )[..12]
        );

    /// <summary>
    /// ⚠ <b>The version, measured.</b> docs/plan/18-versioning-and-release.md.
    /// </summary>
    /// <remarks>
    /// Materialises the previous release's tree beside this one, builds <b>its</b> tool, and runs
    /// the five detectors over the pair: the corpus formatted by both binaries, the rule catalogue,
    /// the exit-code table and the codes the binaries actually produce, the SARIF each one writes,
    /// and the option registry. The number falls out of the highest verdict.
    /// <para>
    /// ⚠ It computes and writes and does nothing else. No tag, no push, no publish — see
    /// <c>.github/workflows/release.yml</c>, which creates the tag inside the job and leaves the
    /// publish behind a flag a person sets.
    /// </para>
    /// <para>
    /// ⚠ With no baseline it reports every surface as <i>unmeasured</i> rather than as unchanged.
    /// The first release cannot be measured — there is nothing to measure against — and a pipeline
    /// that said "no change" there would be making its loudest claim at the one moment it knows
    /// least.
    /// </para>
    /// </remarks>
    Target ReleasePlan =>
        definition => definition
            .DependsOn(Compile)
            .Executes(() => {
                    // ⚠ Release on both sides, or the measurement is of the configuration.
                    // `ToolAssembly` names `bin/Release` for the baseline unconditionally, because
                    // it is the previous release's binary; a Debug candidate would then be compared
                    // against a Release baseline, and that difference is not a compatibility change.
                    if (Configuration != Configuration.Release) {
                        throw new System.InvalidOperationException(
                            "ReleasePlan measures two binaries and both must be Release; this build is "
                            + Configuration
                            + ". Pass --configuration Release."
                        );
                    }

                    var reference = string.IsNullOrEmpty(BaselineRef) ? HighestReleaseTag() : BaselineRef;
                    var arguments = new List<string> {
                        "plan",
                        "--candidate",
                        RootDirectory,
                        "--candidate-tool",
                        ToolAssembly(RootDirectory),
                        "--out",
                        ReleaseDirectory,
                        "--work",
                        ReleaseScratch / "work",
                        "--commit",
                        Output(Nuke.Common.Tools.Git.GitTasks.Git("rev-parse --short HEAD"))
                    };

                    if (Release) {
                        arguments.Add("--release");
                    }

                    // A baseline that is already on disk and already built is taken as it stands;
                    // otherwise one is materialised from the reference. ⚠ The first form is what
                    // the workflow uses, because the second is the unreliable one.
                    if (!string.IsNullOrEmpty(BaselineDirectory)) {
                        if (string.IsNullOrEmpty(BaselineVersion)) {
                            throw new System.InvalidOperationException(
                                "--baseline-directory needs --baseline-version; the number a directory "
                                + "published is not derivable from its path."
                            );
                        }

                        arguments.AddRange(
                            [
                                "--baseline", BaselineDirectory,
                                "--baseline-tool",
                                string.IsNullOrEmpty(BaselineToolPath)
                                    ? ToolAssembly((AbsolutePath)BaselineDirectory).ToString()
                                    : BaselineToolPath,
                                "--baseline-version", BaselineVersion,
                                "--height", Height ?? CommitsSince("v" + BaselineVersion)
                            ]
                        );
                    } else if (string.IsNullOrEmpty(reference)) {
                        Serilog.Log.Warning(
                            "No `v*` tag, no --baseline-ref and no --baseline-directory: every surface will "
                            + "report as unmeasured. That is correct for the first release and wrong for any "
                            + "other."
                        );
                    } else {
                        var baseline = Materialise(reference);
                        arguments.AddRange(
                            [
                                "--baseline", baseline,
                                "--baseline-tool", ToolAssembly(baseline),
                                "--baseline-version",
                                string.IsNullOrEmpty(BaselineVersion) ? reference.TrimStart('v') : BaselineVersion,
                                "--height",
                                Output(Nuke.Common.Tools.Git.GitTasks.Git($"rev-list --count {reference}..HEAD"))
                            ]
                        );
                    }

                    // ⚠ Through the project rather than as a quoted command line. Nuke's `DotNet(string)`
                    // takes one argument string and re-quotes it whole, which turned the twelve arguments
                    // below into a single path that does not exist.
                    DotNetRun(settings => settings
                            .SetProjectFile(
                                RootDirectory / "build" / "Rikarin.Skala.Release" / "Rikarin.Skala.Release.csproj"
                            )
                            .SetConfiguration(Configuration)
                            .EnableNoBuild()
                            .EnableNoRestore()
                            .SetApplicationArguments([.. arguments])
                    );
                }
            );

    /// <summary>
    /// The whole release, up to and not including the publish.
    /// </summary>
    /// <remarks>
    /// ⚠ The last step prints the manifest and stops. Pushing to NuGet is outward-facing and
    /// irreversible, and doc 18 § "Armed, not firing" puts it behind a flag a person sets in the
    /// workflow — not behind a target somebody can reach with a typo.
    /// </remarks>
    Target ReleaseDryRun =>
        definition => definition
            .DependsOn(ReleasePlan, Pack)
            .Executes(() => {
                    var version = (ReleaseDirectory / "version.json").ReadAllText();
                    Serilog.Log.Information("{Version}", version);

                    Serilog.Log.Information("What a release would publish, and does not:");
                    foreach (var package in (RootDirectory / "artifacts" / "packages").GlobFiles("*.nupkg")
                                 .OrderBy(static path => path.Name)) {
                        Serilog.Log.Information(
                            "  {Package} — {Size:N0} bytes",
                            package.Name,
                            new System.IO.FileInfo(package).Length
                        );
                    }

                    Serilog.Log.Information(
                        "Nothing was tagged, pushed or published. docs/plan/18 § \"Armed, not firing\"."
                    );
                }
            );

    AbsolutePath ReleaseTool =>
        RootDirectory / "build" / "Rikarin.Skala.Release" / "bin" / Configuration / "net10.0" / "skala-release.dll";

    static AbsolutePath ToolAssembly(AbsolutePath root) =>
        root / "Tools" / "Rikarin.Skala.Cli" / "bin" / "Release" / "net10.0" / "skala-tool.dll";

    /// <summary>
    /// The previous release's tree, extracted beside this one and built.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>git archive</c> rather than a second worktree: a worktree mutates the repository's
    /// worktree list, and this runs on developer machines that already have several. The tool is
    /// built in <b>Release</b> whatever this build's configuration is, because it is the previous
    /// release's binary and a Debug build of it would measure the configuration.
    /// </remarks>
    AbsolutePath Materialise(string reference) {
        var baseline = ReleaseScratch / "baseline";
        baseline.CreateOrCleanDirectory();

        var archive = ReleaseScratch / "baseline.tar";
        Nuke.Common.Tools.Git.GitTasks.Git($"archive --format=tar --output=\"{archive}\" {reference}");
        Run("tar", $"-xf \"{archive}\" -C \"{baseline}\"");

        archive.DeleteFile();

        var sources = baseline.GlobFiles("**/*.csproj").Count;
        Serilog.Log.Information(
            "{Reference} extracted to {Baseline} — {Projects} projects",
            reference,
            baseline,
            sources
        );
        if (sources < 10) {
            throw new System.InvalidOperationException(
                $"'{reference}' extracted to {sources} project(s). A partial checkout would be measured as a "
                + "release that deleted most of the tool."
            );
        }

        // ⚠ One `dotnet build`, with its own restore, written out as a command line and logged.
        //
        // It was `DotNetRestore` + `DotNetBuild`, then an explicit `dotnet restore` followed by
        // `dotnet build --no-restore`, and both failed every run with `CS0234: 'Options' does not
        // exist in the namespace 'Rikarin.Skala'` — after four seconds, which is less time than the
        // build takes, so the reference closure was never built. ⚠ **`--no-restore` is what breaks
        // it**: a `dotnet restore` of the CLI alone leaves the tree in a state a subsequent
        // `--no-restore` build cannot resolve its ProjectReferences from, and the same tree builds
        // clean the moment the flag comes off. The saving was two seconds against a measurement
        // nobody could run.
        //
        // The invocation is written out rather than driven through the NUKE task so that what runs
        // is what is printed, and the printed line is one a person can paste when this next goes
        // wrong.
        // ⚠ **The whole solution, not the CLI project.** Building
        // `Tools/Rikarin.Skala.Cli/Rikarin.Skala.Cli.csproj` alone fails, intermittently and
        // reproducibly enough to stop every release, with `CS0234: 'Options' does not exist in the
        // namespace 'Rikarin.Skala'` — on exactly the two projects the CLI reaches
        // **transitively**: `Rikarin.Skala.Options` through Core and `Rikarin.Skala.Rules` through
        // Analysis. All twelve referenced projects are reported as built in the same log, moments
        // before the failure. It is not the environment (measured: the child's differs only in
        // `DOTNET_HOST_PATH`, `DOTNET_ROOT_ARM64` and `_MSBUILDTLENABLED`), not node reuse, not
        // `--no-restore`, not the working directory, and not the extraction — the tree is
        // byte-identical to one that builds.
        //
        // A solution build has an explicit, complete project graph and no transitive inference to
        // get wrong, and it is what CI builds for the candidate anyway. The extra minute buys a
        // measurement that runs.
        //
        // ⚠ Through a script and a shell so that what runs is a file a person can read and execute
        // unchanged when this next goes wrong.
        var script = ReleaseScratch / "build-baseline.sh";
        System.IO.File.WriteAllBytes(
            script.ToString(),
            System.Text.Encoding.UTF8.GetBytes(
                // ⚠ `.ToString()` on the paths, and not for tidiness: `string + AbsolutePath` binds
                // to AbsolutePath's own operator, which converts the *left* operand to a path and
                // asserts it is rooted — so concatenating a script body with a path throws
                // `Path '#!/usr/bin/env bash…' must be rooted`.
                "#!/usr/bin/env bash\nset -euo pipefail\ncd \""
                + baseline.ToString()
                + "\"\nexec dotnet build \""
                + (baseline / "Skala.slnx").ToString()
                + "\" --configuration Release\n"
            )
        );

        Run("/bin/bash", $"\"{script}\"", baseline);

        return baseline;
    }

    /// <summary>
    /// One external command, logged, with a non-zero exit turned into a stop — and with this
    /// process's MSBuild environment kept out of it.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The scrubbed variables are why the baseline build works at all.</b> NUKE evaluates
    /// <c>Skala.slnx</c> through <c>Microsoft.Build</c> in its own process, which registers an
    /// MSBuild instance and exports <c>MSBUILD_EXE_PATH</c>, <c>MSBuildExtensionsPath</c> and
    /// <c>MSBuildSDKsPath</c> into the environment every child then inherits. In this repository
    /// that evaluation does not even succeed — <c>_build</c> pins <c>NuGet.Packaging</c> forward for
    /// its advisories, so <c>[MSBuild]::GetTargetFrameworkIdentifier</c> throws
    /// <c>Could not load file or assembly 'NuGet.Frameworks, Version=7.9.0.0'</c> and NUKE logs it
    /// as suppressed. The half-registered state still leaks, and a <c>dotnet build</c> of a
    /// <b>freshly extracted</b> tree then fails to resolve its <c>ProjectReference</c>s:
    /// <c>CS0234: 'Options' does not exist in the namespace 'Rikarin.Skala'</c>. The candidate's own
    /// build survives it because its <c>obj/</c> is already populated, which is what made this look
    /// like a broken baseline for an hour.
    /// </remarks>
    static void Run(string tool, string arguments, string? workingDirectory = null) {
        Serilog.Log.Information("{Tool} {Arguments}", tool, arguments);

        var environment = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables()) {
            var name = (string)entry.Key;
            if (!name.StartsWith("MSBuild", System.StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("MSBUILD", System.StringComparison.Ordinal)) {
                environment[name] = entry.Value?.ToString() ?? "";
            }
        }

        using var process = Nuke.Common.Tooling.ProcessTasks.StartProcess(
            tool,
            arguments,
            workingDirectory,
            environment
        );

        process.WaitForExit();
        if (process.ExitCode != 0) {
            throw new System.InvalidOperationException($"`{tool} {arguments}` exited {process.ExitCode}.");
        }
    }

    /// <summary>
    /// Commits on this branch since <paramref name="reference"/>, or <c>0</c> when it is not in the
    /// clone.
    /// </summary>
    /// <remarks>
    /// ⚠ A missing tag is <c>0</c> rather than a stop. `--baseline-directory` names a *tree*, and a
    /// tree can be a release that was never tagged — which is the state this repository is in, and
    /// which turned the first run of that path into `git exited 128`. A pre-release counter of 0 is
    /// wrong-but-harmless on a dry run and is never what a real release publishes; a release with no
    /// tag to count from is the case doc 18 § "The number" resolves by not tagging `master` at all.
    /// </remarks>
    static string CommitsSince(string reference) {
        try {
            return Output(Nuke.Common.Tools.Git.GitTasks.Git($"rev-list --count {reference}..HEAD"));
        } catch (System.Exception exception) {
            Serilog.Log.Warning(
                "No commit count from '{Reference}' ({Message}); the pre-release counter is 0.",
                reference,
                exception.Message
            );

            return "0";
        }
    }

    /// <summary>The highest <c>v*</c> tag by version order, or empty when there is none.</summary>
    static string HighestReleaseTag() =>
        Output(Nuke.Common.Tools.Git.GitTasks.Git("tag --list v* --sort=-v:refname"))
            .Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.Length > 0)
        ?? "";

    static string Output(IReadOnlyCollection<Nuke.Common.Tooling.Output> lines) =>
        string.Join('\n', lines.Select(static line => line.Text)).Trim();

    void Skala(params object[] arguments) {
        var cli = RootDirectory / "Tools" / "Rikarin.Skala.Cli" / "Rikarin.Skala.Cli.csproj";
        DotNetRun(settings => settings
                .SetProjectFile(cli)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetApplicationArguments(arguments.Select(static argument => argument.ToString()!).ToArray())
        );
    }
}
