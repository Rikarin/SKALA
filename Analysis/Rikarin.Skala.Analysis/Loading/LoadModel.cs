using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>What to load, and how.</summary>
public sealed record LoadRequest {
    public required string RepositoryRoot { get; init; }

    public LoadMode Mode { get; init; } = LoadMode.Binlog;

    /// <summary>Where the binlog is. Null means look in the usual places.</summary>
    public string? BinlogPath { get; init; }

    /// <summary>The solution or project for <see cref="LoadMode.Workspace" />. Null means find one.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>⚠ Fail rather than analyse against a binlog older than the sources.</summary>
    public bool RequireFreshBinlog { get; init; }

    /// <summary>For <see cref="LoadMode.Loose" />: the files or directories to parse.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>Extra preprocessor symbols, from <c>--define</c>.</summary>
    public IReadOnlyList<string> Define { get; init; } = [];

    /// <summary>
    ///     ⚠ Fall through to a lesser mode rather than failing. Default on, because "I asked for binlog
    ///     and there is no binlog" is the common case for the agent path and a hard failure there means
    ///     the agent gets nothing rather than the syntactic half.
    /// </summary>
    public bool AllowFallback { get; init; } = true;
}

/// <summary>
///     One <see cref="Compilation" />, with everything about how it was obtained that the report needs.
/// </summary>
/// <remarks>
///     ⚠ <see cref="ReportablePaths" /> is not <see cref="Compilation.SyntaxTrees" />. Generated sources
///     are <em>analysed</em> — they are part of the program and leaving them out changes what the
///     semantic model says — and never <em>reported on</em>, because a diagnostic in a file the user
///     cannot edit is noise (docs/plan/07 § binlog, "Generated sources").
/// </remarks>
public sealed record CompilationUnit {
    public required string Name { get; init; }

    public required CSharpCompilation Compilation { get; init; }

    /// <summary>The target framework moniker, when the load mode knows one.</summary>
    public string TargetFramework { get; init; } = string.Empty;

    public ImmutableArray<string> PreprocessorSymbols { get; init; } = [];

    /// <summary>The files findings may be reported against: sources, minus generated ones.</summary>
    public ImmutableHashSet<string> ReportablePaths { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    ///     The files the load was asked for and could not open (<c>SK9015</c>). Counted, never
    ///     iterated.
    /// </summary>
    /// <remarks>
    ///     ⚠ #356. <see cref="ReportablePaths" /> answers two questions that used to be one: which
    ///     files the stages read, and how many files the run was about. A loader that added a path
    ///     only <em>after</em> reading it answered the first correctly and the second wrong — an
    ///     unreadable file never entered the denominator, so a two-file tree with one mode-000 file
    ///     printed <c>1 of 1 file was not checked</c>, and <c>verify</c>'s trailer said
    ///     <c>0 files were checked</c> directly under a finding on the readable neighbour. The
    ///     fraction exists to say the other files were covered (#345); a denominator that excludes
    ///     the uncovered files says the opposite.
    ///     <para>
    ///         The two questions are separated rather than the set widened, because every stage —
    ///         formatting, arrangement, duplication, the scope filter of #346 — iterates
    ///         <see cref="ReportablePaths" /> and would otherwise open the file again and report it
    ///         again. A path is in exactly one of the two sets: <c>CheckCommand</c> sums both for
    ///         <c>RunReport.FileCount</c> and hands only this one's complement to the stages. The loader
    ///         that put a path here also emitted the <c>SK9015</c> against it, once.
    ///     </para>
    /// </remarks>
    public ImmutableHashSet<string> UnreadablePaths { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>Analyzer assemblies this compilation's build referenced (ADR-008 hosts these too).</summary>
    public ImmutableArray<string> AnalyzerReferences { get; init; } = [];

    /// <summary>The <c>.editorconfig</c> files the build passed to the compiler.</summary>
    public ImmutableArray<string> AnalyzerConfigPaths { get; init; } = [];

    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    ///     The same project's <em>other</em> target frameworks (#343). Empty for a single-target
    ///     project and for the loose load.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A multi-targeted project is several compilations over one set of source files</b>, and
    ///     nothing an analyzer can see says so. Every loader opens one <see cref="CompilationUnit" />
    ///     per moniker and <c>CheckCommand</c> runs the analyzers over each in turn, so a rule whose
    ///     condition is framework-dependent fires from whichever moniker satisfies it and its fix is
    ///     written to a file all of them compile. <c>MultiTargetLink.Apply</c> fills this in for every
    ///     mode at once, and <c>Hosting.EditorConfigOptions</c> publishes it to the driver.
    /// </remarks>
    public ImmutableArray<CSharpCompilation> Siblings { get; init; } = [];
}

/// <summary>The result of loading: compilations, and everything that went wrong on the way.</summary>
public sealed record LoadedProject {
    public required LoadMode Mode { get; init; }

    public ImmutableArray<CompilationUnit> Units { get; init; } = [];

    /// <summary>
    ///     ⚠ Surfaced verbatim, never swallowed. A partially-loaded workspace that silently analyses
    ///     half a solution is the thing to avoid, and the only way to avoid it is to say so.
    /// </summary>
    public ImmutableArray<SkalaDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>One line for the report header: what was loaded and from where.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    ///     ⚠ <b>The loader itself could not run</b> — as distinct from running and finding nothing.
    /// </summary>
    /// <remarks>
    ///     The two are opposite situations that produce the same empty <see cref="Units" />, and
    ///     conflating them is how a gate reports green over code it never opened. "There is no binlog
    ///     here" is a fact about the repository and falling through to the next mode is the right
    ///     answer. "MSBuild could not be located", "the assembly holding <c>MSBuildWorkspace</c> is not
    ///     beside the tool" and "this .csproj could not be opened" are facts about the *tool*, and no
    ///     amount of falling through makes the answer they were supposed to produce appear.
    ///     <para>
    ///         ⚠ Set this only for the second kind. <see cref="ProjectLoader" /> stops the ladder on it
    ///         when the caller asked for that mode by name, and <c>CheckCommand</c> then reports
    ///         <c>ExitCodes.LoadFailure</c> rather than the syntactic half of an answer at exit 0. The
    ///         defect that put it here: <c>Microsoft.CodeAnalysis.Workspaces.MSBuild</c> shipped with
    ///         <c>ExcludeAssets=runtime</c>, so the workspace mode threw on its first line, every run
    ///         fell through to loose, and a consumer's `check` gate passed having never built a
    ///         compilation.
    ///     </para>
    ///     <para>
    ///         ⚠ #361: when the failed rung is <em>not</em> the one the caller named — workspace under
    ///         the default binlog ladder — the ladder does continue to loose, and the failure travels
    ///         with it: the rung's error-severity diagnostics stay in <see cref="Diagnostics" /> and
    ///         <c>Gate.EvaluateReliability</c> fails the verdict on them at exit 1. The syntactic half
    ///         is delivered; it is not allowed to pass as the whole.
    ///     </para>
    /// </remarks>
    public bool Failed { get; init; }

    public bool IsEmpty => Units.IsEmpty;
}

/// <summary>Parsing <c>--load</c>.</summary>
public static class LoadModes {
    public static LoadMode Parse(string? value) =>
        value?.ToLowerInvariant() switch {
            "workspace" => LoadMode.Workspace,
            "loose" => LoadMode.Loose,
            _ => LoadMode.Binlog
        };

    public static bool TryParse(string? value, out LoadMode mode) {
        switch (value?.ToLowerInvariant()) {
            case "binlog":
                mode = LoadMode.Binlog;
                return true;

            case "workspace":
                mode = LoadMode.Workspace;
                return true;

            case "loose":
                mode = LoadMode.Loose;
                return true;

            default:
                mode = LoadMode.Binlog;
                return false;
        }
    }
}
