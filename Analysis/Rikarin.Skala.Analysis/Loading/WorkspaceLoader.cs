using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     The fallback (ADR-007): <c>MSBuildLocator</c> plus <c>MSBuildWorkspace</c>.
/// </summary>
/// <remarks>
///     Present because "I have a solution and no binlog" is a real situation. It is slower, it is
///     sensitive to custom targets, and — the point of the whole type —
///     ⚠ <b>its <c>WorkspaceDiagnostics</c> are surfaced verbatim rather than swallowed</b>. A
///     partially-loaded workspace that silently analyses half a solution produces a clean report about
///     a third of the code, and nothing in that report says so unless this does.
/// </remarks>
public static class WorkspaceLoader {
    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var resolution = Resolve(request);
        if (resolution.Error is { } error) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    error,
                    request.ProjectPath ?? request.RepositoryRoot
                )
            );

            return new() { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true };
        }

        var target = resolution.Target;
        if (target is null) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Warning,
                    "no .slnx, .sln or .csproj was found to load",
                    request.RepositoryRoot
                )
            );

            return new() { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable() };
        }

        if (!MSBuildRuntime.Ensure(out var locatorError)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"no MSBuild could be located: {locatorError}",
                    target
                )
            );

            return new() { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true };
        }

        try {
            return LoadCore(request, target, diagnostics, cancellation);
        } catch (Exception exception) when (exception is FileNotFoundException
                                                or FileLoadException
                                                or TypeLoadException
                                                or BadImageFormatException) {
            // ⚠ **The tool is broken, and this is the branch that has to say so out loud.** Every
            // exception here is a missing or unloadable assembly, which reaches this frame rather
            // than LoadCore's own `catch` because the JIT resolves LoadCore's MSBuildWorkspace
            // reference while preparing the method — before its first line runs.
            //
            // It shipped: `Microsoft.CodeAnalysis.Workspaces.MSBuild` carried `ExcludeAssets=runtime`
            // (it is Roslyn's assembly, not MSBuild's, so the SDK never supplied a copy), and the
            // FileNotFoundException escaped as far as the CLI's catch-all, which called it an
            // internal error. Under `SkalaMode=check` that is a warning and a green build.
            //
            // `Failed` is what stops the loose fallback from turning it back into exit 0. The
            // reference is fixed; this exists because the next dependency to go missing must produce
            // a load failure that names itself, not a pass.
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"the workspace loader could not be initialised — a required assembly is missing from the Skala installation: {exception.Message}",
                    target
                )
            );

            return new() { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true };
        }
    }

    /// <summary>
    ///     ⚠ Isolated behind a method that is not inlined into <see cref="Load" />.
    /// </summary>
    /// <remarks>
    ///     <c>MSBuildLocator</c> resolves MSBuild assemblies through an <c>AssemblyResolve</c> handler
    ///     installed by <c>RegisterDefaults</c>. If the JIT has already prepared a method that
    ///     references <c>MSBuildWorkspace</c>, the load happens before the handler exists and fails with
    ///     a message about a file that is obviously present. Keeping every workspace type behind a
    ///     separate frame is the documented workaround and the reason this method exists.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static LoadedProject LoadCore(
        LoadRequest request,
        string target,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CancellationToken cancellation
    ) {
        using var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;

        try {
            if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
                workspace.OpenProjectAsync(target, cancellationToken: cancellation).GetAwaiter().GetResult();
            } else {
                workspace.OpenSolutionAsync(target, cancellationToken: cancellation).GetAwaiter().GetResult();
            }
        } catch (Exception exception) when (exception is IOException
                                                or InvalidOperationException
                                                or NotSupportedException) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"'{target}' could not be opened: {exception.Message}",
                    target
                )
            );

            // ⚠ A named target that will not open is a failure, not an absence: the caller pointed at
            // a solution and it could not be read. Falling through to the syntactic loader here is
            // what produced a clean report over a solution nobody had managed to load.
            return new() { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true };
        }

        // ⚠ Verbatim. Every one of these is a project that did not load, or loaded without its
        // references; the difference between "clean" and "not analysed" lives here.
        foreach (var diagnostic in workspace.Diagnostics) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? SkalaSeverity.Warning : SkalaSeverity.Info,
                    "workspace: " + diagnostic.Message,
                    target
                )
            );
        }

        var units = ImmutableArray.CreateBuilder<CompilationUnit>();
        foreach (var project in workspace.CurrentSolution.Projects) {
            if (project.Language != LanguageNames.CSharp) {
                continue;
            }

            if (project.GetCompilationAsync(cancellation).GetAwaiter().GetResult()
                is not CSharpCompilation compilation) {
                continue;
            }

            var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            foreach (var document in project.Documents) {
                if (document.FilePath is { Length: > 0 } path && !BinlogLoader.IsGenerated(path)) {
                    reportable.Add(Path.GetFullPath(path));
                }
            }

            var parseOptions = project.ParseOptions as CSharpParseOptions;
            units.Add(
                new CompilationUnit {
                    Name = project.Name,
                    Compilation = compilation,
                    TargetFramework = project.Name.Contains('(', StringComparison.Ordinal)
                        ? project.Name[(project.Name.IndexOf('(', StringComparison.Ordinal) + 1)..].TrimEnd(')')
                        : string.Empty,
                    PreprocessorSymbols = parseOptions is null ? [] : [.. parseOptions.PreprocessorSymbolNames],
                    ReportablePaths = reportable.ToImmutable(),
                    AnalyzerReferences = [
                        .. project.AnalyzerReferences
                            .Select(static reference => reference.FullPath ?? string.Empty)
                            .Where(static path => path.Length > 0)
                    ],
                    ProjectPath = project.FilePath ?? string.Empty
                }
            );
        }

        // ⚠ **Nothing analysable plus a failure diagnostic is a failed load, not an empty one**, and
        // telling those two apart is this type's stated purpose applied one level up. The paragraph
        // above surfaces the failures verbatim; surfacing them is not the same as acting on them, and
        // until this block nothing downstream read a single one.
        //
        // ⚠ It counts *documents*, not projects, and that is the entire subtlety. `MSBuildWorkspace`
        // does not throw when a project will not evaluate — an unresolvable SDK, a custom target that
        // errors, an unrestored reference. It records the failure in `Diagnostics` and hands back a
        // placeholder project with **no documents in it**, so a project count of 1 is not evidence
        // that anything loaded. Measured on a .csproj naming an SDK that does not exist: one
        // `WorkspaceDiagnosticKind.Failure` reading "Msbuild failed when processing the file", one
        // project, zero documents, zero findings, **exit 0** — a gate reporting a clean tree over a
        // project MSBuild had just refused to evaluate.
        //
        // A *partial* load — some projects in, some out — deliberately stays a warning. Refusing
        // there would make the gate unsatisfiable on any repository holding one unbuildable project,
        // which is the mistake BinlogLoader.CoverageSeverity documents at length. The line drawn here
        // is the narrow one: not one line of code came back, and something failed.
        var analysable = units.Sum(static unit => unit.ReportablePaths.Count);
        var failedOutright = analysable == 0
            && workspace.Diagnostics.Exists(static diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure);

        if (failedOutright) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"'{target}' yielded no analysable source; every project in it failed to load",
                    target
                )
            );
        }

        var degraded = !failedOutright && ReportMissingAnalyzerAssemblies(units, target, diagnostics);

        return new() {
            Mode = LoadMode.Workspace,

            // ⚠ Emptied rather than merely flagged. `arrange` and `check` both keep running over
            // whatever compilations they are handed and only consult the diagnostics for the exit
            // code, so returning the units alongside an error still prints 353 confident rewrites —
            // with a non-zero exit stapled to them, which is not what "refusing" means. Handing back
            // nothing is the refusal; the two commands then have only the syntactic subset, which is
            // sound because it never asks a question the missing assemblies would have answered.
            Units = degraded ? [] : units.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable(),
            Failed = failedOutright || degraded,
            Summary =
                $"workspace {Path.GetFileName(target)} ({units.Count.ToString(CultureInfo.InvariantCulture)} project(s), {workspace.Diagnostics.Count.ToString(CultureInfo.InvariantCulture)} workspace diagnostic(s))"
        };
    }

    /// <summary>
    ///     ⚠
    ///     <b>
    ///         A workspace load whose generators are not on disk is refused, because its findings are
    ///         wrong rather than missing (#336).
    ///     </b>
    /// </summary>
    /// <remarks>
    ///     <c>MSBuildWorkspace</c> evaluates the <c>Debug</c> configuration unless told otherwise, so
    ///     on a checkout built only in <c>Release</c> every analyzer path it reports points into a
    ///     <c>bin/Debug</c> that does not exist. Roslyn loads no generator from a file that is not
    ///     there and says nothing, and the compilation then lacks every generated member — which is
    ///     not a smaller answer but a different program.
    ///     <para>
    ///         ⚠ <b>Measured, and the number is why this is <c>Failed</c> rather than a warning.</b> A
    ///         fresh clone of this repository built only in <c>Release</c>:
    ///         <c>arrange --check --load=workspace</c> reported
    ///         <b>
    ///             353 files to rewrite, all of them
    ///             <c>SK0210 usings</c>
    ///         </b>; the same clone through a binlog reported <b>0</b>. The
    ///         proposed rewrite deletes <c>using Rikarin.Skala.Rules.Metadata;</c> from files calling
    ///         <c>RuleCatalog.All</c>, so taking the tool's advice does not compile. A confident wrong
    ///         finding carrying a build-breaking fix is worse than a silent zero, and both are worse
    ///         than a refusal that names the missing file.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>This is not the mechanism #336 was filed with, and that diagnosis is refuted.</b>
    ///         The issue attributed the 353 findings to <c>MSBuildWorkspace</c> dropping the
    ///         <c>Rikarin.Skala.Rules.Metadata</c> project reference — the <c>SK9024</c> line reading
    ///         "Found project reference without a matching metadata reference". Building the same
    ///         clone in <c>Debug</c> takes <c>arrange</c> to <b>0 files</b> while <em>all three</em> of
    ///         those <c>SK9024</c> lines are still reported, <c>Rules.Metadata</c> among them. The
    ///         dropped reference is benign and permanent; the unbuilt configuration is the defect. It
    ///         also is not confined to <c>Rules.Metadata</c>: <b>44 of the 353</b> files carry no
    ///         reference to that project at all and were flagged because
    ///         <c>Rikarin.Skala.Options.Generator</c> — a second generator, whose project reference
    ///         resolves perfectly — had likewise never been built into <c>Debug</c>.
    ///     </para>
    ///     <para>
    ///         The escapes are named in the message: build the configuration, or use
    ///         <c>--load=binlog</c>, which reads what a build actually compiled (ADR-007).
    ///     </para>
    /// </remarks>
    static bool ReportMissingAnalyzerAssemblies(
        ImmutableArray<CompilationUnit>.Builder units,
        string target,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        if (!GeneratorDriver.ReportMissingAssemblies(
                units.SelectMany(static unit => unit.AnalyzerReferences),
                target,
                diagnostics,
                SkalaSeverity.Error
            )) {
            return false;
        }

        diagnostics.Add(
            new SkalaDiagnostic(
                ConfigDiagnosticIds.AnalyzerAssemblyMissing,
                SkalaSeverity.Error,
                $"refusing to analyse '{Path.GetFileName(target)}': the assemblies above are missing, so the "
                + "generated half of the program is absent and every semantic answer over this load is "
                + "unsound. MSBuildWorkspace evaluates Debug unless told otherwise — build that "
                + "configuration, or use --load=binlog, which reads what a build actually compiled.",
                target
            )
        );

        return true;
    }

    /// <summary>
    ///     Which solution or project this workspace load is about.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>#284: the positional path used to be ignored entirely.</b> Resolution went
    ///     <c>--project</c>, then a glob of <see cref="LoadRequest.RepositoryRoot" /> — the working
    ///     directory — and <c>skala check /tmp/probe --load=workspace</c> run from inside this checkout
    ///     therefore loaded <c>Skala.slnx</c> and produced a clean, well-formed, entirely plausible
    ///     report <em>about Skala</em>. Nothing in it named the tree it had analysed.
    ///     <para>
    ///         That is the failure mode this codebase's own instrument-verification step is built to
    ///         catch and could not: dropping a probe in and confirming a rule fires returns no findings
    ///         under <c>workspace</c>, which reads as "the rule is dead" rather than "the loader went
    ///         somewhere else". At least one rule was very nearly withdrawn on that evidence.
    ///     </para>
    ///     <para>
    ///         ⚠ The fallback to <see cref="LoadRequest.RepositoryRoot" /> is kept only where it cannot
    ///         change which tree is measured — that is, where the requested path lies <em>inside</em> the
    ///         repository root, which is the ordinary <c>skala check src --load=workspace</c> case:
    ///         load the solution, report on a subtree, and <c>CheckCommand.Paths</c> does the filtering.
    ///         A requested path <em>outside</em> the root is refused instead. A refusal is a bad
    ///         experience; a clean report about the wrong repository is a wrong answer.
    ///     </para>
    /// </remarks>
    internal static WorkspaceTargetResolution Resolve(LoadRequest request) {
        if (request.ProjectPath is { Length: > 0 } named) {
            var path = Path.GetFullPath(named);
            return File.Exists(path)
                ? new WorkspaceTargetResolution(path, null)
                : new WorkspaceTargetResolution(null, $"workspace target '{path}' does not exist");
        }

        var root = Path.GetFullPath(request.RepositoryRoot);
        foreach (var requested in request.Paths) {
            var full = Path.GetFullPath(requested);

            // The path may name the workspace target itself — `check ./Probe.csproj --load=workspace`.
            if (File.Exists(full)) {
                if (WorkspaceExtensions.Contains(Path.GetExtension(full), StringComparer.OrdinalIgnoreCase)) {
                    return new(full, null);
                }

                full = Path.GetDirectoryName(full) ?? full;
            }

            if (!Directory.Exists(full)) {
                continue;
            }

            var under = SearchIn(full);
            if (under.ShouldAttemptWorkspace) {
                return under;
            }

            // ⚠ Nothing under a path that is not part of this root. Falling back here is what
            // silently swapped the tree, so it is refused, and the message names both trees because
            // the whole defect was that the output never said which one had been read.
            if (!IsWithin(full, root)) {
                return new(
                    null,
                    $"no .slnx, .sln or .csproj was found under '{full}', and it is outside the repository root "
                    + $"'{root}'. Refusing to fall back: that would analyse '{Path.GetFileName(root)}' and report "
                    + "it as though it were the requested path. Name the target with --project, or use --load=loose.",
                    true
                );
            }
        }

        return SearchIn(root);
    }

    /// <summary>⚠ .slnx before .sln: a stale .sln beside it would load a different set of projects.</summary>
    static WorkspaceTargetResolution SearchIn(string directory) {
        foreach (var pattern in new[] { "*.slnx", "*.sln", "*.csproj" }) {
            var matches = Directory.GetFiles(directory, pattern)
                .OrderBy(
                    static file => file,
                    StringComparer.Ordinal
                )
                .ToArray();

            if (matches.Length == 1) {
                return new(matches[0], null);
            }

            if (matches.Length > 1) {
                return new(
                    null,
                    $"multiple '{pattern}' workspace targets were found; choose one with --project: "
                    + string.Join(", ", matches.Take(3).Select(Path.GetFileName))
                );
            }
        }

        return new(null, null);
    }

    static readonly string[] WorkspaceExtensions = [".slnx", ".sln", ".csproj"];

    static bool IsWithin(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.Ordinal)
        || candidate.StartsWith(
            root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar,
            StringComparison.Ordinal
        );
}

internal sealed record WorkspaceTargetResolution(string? Target, string? Error, bool OutsideRoot = false) {
    /// <summary>
    ///     ⚠ <see cref="OutsideRoot" /> is an error that <c>auto</c> must not treat as a reason to
    ///     choose workspace. <see cref="ProjectLoader.ResolveAutoMode" /> reads this to mean "discovery
    ///     found a target, or found an ambiguity the caller has to resolve" — and the outside-root
    ///     refusal is neither. It says there is genuinely no workspace target under the requested path,
    ///     which is the documented condition for choosing loose; loose then honours that path and reads
    ///     the tree the caller actually named. The refusal still stands for an explicit
    ///     <c>--load=workspace</c>, because there the ladder enters <see cref="WorkspaceLoader.Load" />
    ///     directly and <see cref="Resolve" /> is consulted again.
    /// </summary>
    public bool ShouldAttemptWorkspace => Target is not null || (Error is not null && !OutsideRoot);
}
