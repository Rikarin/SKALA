using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

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

    /// <summary>Analyzer assemblies this compilation's build referenced (ADR-008 hosts these too).</summary>
    public ImmutableArray<string> AnalyzerReferences { get; init; } = [];

    /// <summary>The <c>.editorconfig</c> files the build passed to the compiler.</summary>
    public ImmutableArray<string> AnalyzerConfigPaths { get; init; } = [];

    public string ProjectPath { get; init; } = string.Empty;
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
