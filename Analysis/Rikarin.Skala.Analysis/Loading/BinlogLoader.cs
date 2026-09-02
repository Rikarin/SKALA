using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;
using BuildProject = Microsoft.Build.Logging.StructuredLogger.Project;
using SourceText = Microsoft.CodeAnalysis.Text.SourceText;
using Task = Microsoft.Build.Logging.StructuredLogger.Task;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     ADR-007's primary path: reconstruct compilations from what the compiler was actually told.
/// </summary>
/// <remarks>
///     <code>
/// dotnet build -bl  →  BinaryLog.ReadBuild()  →  every CscTask.CommandLineArguments
///                   →  CSharpCommandLineParser.Default.Parse(...)
///                   →  sources, references, options, analyzers, editorconfigs
///     </code>
///     This is what the build compiled — generated sources included, conditional symbols correct,
///     analyzer references as configured, multi-targeting expressed as one <c>Csc</c> invocation per
///     target framework. No design-time build, no MSBuild evaluation, no SDK-version sensitivity beyond
///     the one that already produced the binlog. ⚠ It is the only option that is <em>definitionally</em>
///     correct, and it costs one real build, which CI is doing anyway.
/// </remarks>
public static class BinlogLoader {
    /// <summary>Where a binlog is looked for when none is named, in the order they are tried.</summary>
    /// <remarks>
    ///     ⚠ <c>ImmutableArray</c> rather than <c>string[]</c>, and the order is the contract. A
    ///     <c>public static readonly string[]</c> is writable by every caller that can see it
    ///     (<c>SK6031</c>): one <c>Conventions[0] = …</c> from anywhere in the process silently
    ///     redirects the default binlog lookup for every subsequent load, and the message at the
    ///     failure site would keep printing the list it no longer searched.
    /// </remarks>
    public static readonly ImmutableArray<string> Conventions = [
        Path.Combine("artifacts", "skala.binlog"), Path.Combine("artifacts", "build.binlog"),
        Path.Combine(
            "artifacts",
            "msbuild.binlog"
        ), "msbuild.binlog", "build.binlog"
    ];

    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation = default) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var path = Resolve(request);
        if (path is null) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NoBinlog,
                    SkalaSeverity.Warning,
                    "no binary log was found; run `dotnet build -bl:artifacts/skala.binlog`",
                    request.RepositoryRoot,
                    0,
                    "Looked for: " + string.Join(", ", Conventions)
                )
            );

            return new() { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        // ⚠ Before any MSBuild type is touched in this frame. See MSBuildRuntime's remarks.
        if (!MSBuildRuntime.Ensure(out var locatorError)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NoBinlog,
                    SkalaSeverity.Warning,
                    $"the SDK's MSBuild could not be located, so '{path}' cannot be read: {locatorError}",
                    path
                )
            );

            return new() { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        var invocations = new List<(string Project, string Arguments)>();
        if (!TryRead(path, invocations, diagnostics)) {
            return new() { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        var units = ImmutableArray.CreateBuilder<CompilationUnit>();
        foreach (var (project, arguments) in invocations) {
            var unit = Build(project, arguments, request, diagnostics, cancellation);
            if (unit is not null) {
                units.Add(unit);
            }
        }

        ReportStaleness(path, units, request, diagnostics);

        return new() {
            Mode = LoadMode.Binlog,
            Units = units.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable(),
            Summary =
                $"binlog {Relative(request.RepositoryRoot, path)} ({units.Count.ToString(CultureInfo.InvariantCulture)} compilation(s))"
        };
    }

    /// <summary>
    ///     ⚠ Not inlined, so that <see cref="MSBuildRuntime.Ensure" /> has run before the JIT resolves
    ///     this frame's references to MSBuild's types.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static bool TryRead(
        string path,
        List<(string Project, string Arguments)> invocations,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        try {
            Walk(BinaryLog.ReadBuild(path), invocations);
            return true;
        } catch (Exception exception) when (exception is IOException
                                                or InvalidDataException
                                                or NotSupportedException
                                                or ArgumentException
                                                or FileNotFoundException
                                                or FileLoadException
                                                or BadImageFormatException) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NoBinlog,
                    SkalaSeverity.Warning,
                    $"'{path}' could not be read: {exception.Message}",
                    path
                )
            );

            return false;
        }
    }

    static string? Resolve(LoadRequest request) {
        if (request.BinlogPath is { Length: > 0 } named) {
            return File.Exists(named) ? Path.GetFullPath(named) : null;
        }

        foreach (var convention in Conventions) {
            var candidate = Path.Combine(request.RepositoryRoot, convention);
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Every <c>Csc</c> invocation in the log, with the project that ran it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Recursive over <c>Children</c> rather than through the library's visitor, because the
    ///     visitor's shape has changed between StructuredLogger versions and a walk of a tree of nodes
    ///     is not the part of this worth a dependency on an API surface.
    /// </remarks>
    static void Walk(BaseNode node, List<(string Project, string Arguments)> found) {
        if (node is Task { Name: "Csc" } task && task.CommandLineArguments is { Length: > 0 } arguments) {
            found.Add((ProjectOf(task), arguments));
        }

        if (node is TreeNode tree && tree.HasChildren) {
            foreach (var child in tree.Children) {
                Walk(child, found);
            }
        }
    }

    static string ProjectOf(BaseNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is BuildProject project) {
                return project.ProjectFile ?? string.Empty;
            }
        }

        return string.Empty;
    }

    static CompilationUnit? Build(
        string projectPath,
        string commandLine,
        LoadRequest request,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CancellationToken cancellation
    ) {
        var baseDirectory = projectPath.Length > 0
            ? Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? request.RepositoryRoot
            : request.RepositoryRoot;

        // ⚠ The recorded line starts with the compiler's own path; the parser wants only arguments.
        var arguments = CommandLine.Split(commandLine);
        if (arguments.Count > 0 && arguments[0].EndsWith("csc.dll", StringComparison.OrdinalIgnoreCase)) {
            arguments.RemoveAt(0);
        }

        if (arguments.Count > 0 && Path.GetFileNameWithoutExtension(arguments[0]) is "csc" or "dotnet") {
            arguments.RemoveAt(0);
        }

        CSharpCommandLineArguments parsed;
        try {
            parsed = CSharpCommandLineParser.Default.Parse(arguments, baseDirectory, null);
        } catch (ArgumentException exception) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NoBinlog,
                    SkalaSeverity.Warning,
                    $"a Csc command line could not be parsed: {exception.Message}",
                    projectPath
                )
            );

            return null;
        }

        var trees = ImmutableArray.CreateBuilder<SyntaxTree>();
        var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        var parseOptions = parsed.ParseOptions;

        foreach (var source in parsed.SourceFiles) {
            var full = Path.GetFullPath(source.Path);
            SourceText text;
            try {
                using var stream = File.OpenRead(full);
                text = SourceText.From(stream, canBeEmbedded: false);
            } catch (IOException) {
                // ⚠ SK9020's sibling: the build compiled a file that is no longer on disk. It is
                // dropped from the compilation rather than faked, and reported below.
                continue;
            }

            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, full, cancellation));

            // ⚠ Generated sources are analysed and never reported on: a diagnostic the user cannot
            // fix is noise (docs/plan/07 § binlog, "Generated sources").
            if (!IsGenerated(full)) {
                reportable.Add(full);
            }
        }

        if (trees.Count == 0) {
            return null;
        }

        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var reference in parsed.MetadataReferences) {
            var resolved = MetadataReferenceCache.Get(
                Path.IsPathRooted(reference.Reference)
                    ? reference.Reference
                    : Path.Combine(baseDirectory, reference.Reference),
                reference.Properties.Aliases.IsDefaultOrEmpty ? null : reference.Properties.Aliases
            );

            if (resolved is not null) {
                references.Add(resolved);
            }
        }

        var name = parsed.CompilationName
            ?? Path.GetFileNameWithoutExtension(projectPath)
            ?? "compilation";

        var analyzerReferences = ImmutableArray.CreateBuilder<string>();
        foreach (var reference in parsed.AnalyzerReferences) {
            analyzerReferences.Add(
                Path.IsPathRooted(reference.FilePath)
                    ? reference.FilePath
                    : Path.Combine(baseDirectory, reference.FilePath)
            );
        }

        var compilation = CSharpCompilation.Create(
            name,
            trees.ToImmutable(),
            references.ToImmutable(),
            parsed.CompilationOptions.WithConcurrentBuild(true)
        );

        // ⚠ The generated half of the program. See GeneratorDriver's remarks: without this the
        // compilation is missing every generated member, which on Vixen is 1 675 compiler errors
        // and a semantic model that answers questions about error types.
        var analyzerConfigPaths = ImmutableArray.CreateRange(parsed.AnalyzerConfigPaths);
        compilation = GeneratorDriver.Run(
            compilation,
            analyzerReferences.ToImmutable(),
            [
                .. parsed.AdditionalFiles.Select(file => Path.IsPathRooted(file.Path)
                        ? file.Path
                        : Path.Combine(baseDirectory, file.Path)
                )
            ],
            Hosting.EditorConfigOptions.ProviderFor(analyzerConfigPaths),
            parseOptions,
            diagnostics,
            cancellation
        );

        return new() {
            Name = name,
            Compilation = compilation,
            TargetFramework = TargetFrameworkOf(parsed),
            PreprocessorSymbols = [.. parseOptions.PreprocessorSymbolNames],
            ReportablePaths = reportable.ToImmutable(),
            AnalyzerReferences = analyzerReferences.ToImmutable(),
            AnalyzerConfigPaths = analyzerConfigPaths,
            ProjectPath = projectPath
        };
    }

    /// <summary>
    ///     ⚠ The three ways a binlog lies about the tree, and the second is the dangerous one.
    /// </summary>
    /// <remarks>
    ///     A file whose content moved since the build is <c>SK9020</c> — the current text is what is
    ///     analysed, so the report is about the working tree. A file that exists and is in no
    ///     compilation is <c>SK9021</c>, because it is <em>silently unanalysed</em>: the run comes back
    ///     clean and says nothing about it, which is the worst failure this tool can have.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             The third is incompleteness, and it is why <c>--require-fresh-binlog</c> was not
    ///             enough.
    ///         </b> A binlog from an *incremental* build contains only the projects MSBuild actually
    ///         rebuilt, and it is not stale — its mtime is seconds old. Measured: <c>arrange --check</c>
    ///         against an incremental binlog saw <b>824</b> files to change and left <b>2 147</b> in no
    ///         compilation; against a <c>--no-incremental</c> build's binlog, <b>1 188</b> and <b>79</b>.
    ///         Every command reported success. A gate that analyses a third of the tree and comes back
    ///         green is worse than no gate, because it is believed.
    ///     </para>
    ///     <para>
    ///         Age is a timestamp comparison and cannot see this: the binlog was not stale, it was partial.
    ///         So <c>--require-fresh-binlog</c> now checks <em>coverage</em> as well — the binlog's
    ///         compilation set against the files the path filter selects — and an incomplete binlog under
    ///         that flag is an error rather than a warning somebody reads past. The headline is a ratio,
    ///         because twenty file names and an "and 2 127 more" do not read as "two thirds of your
    ///         repository was not analysed", and that is the sentence.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The percentage of selected source files a binlog must cover before
    ///     <c>--require-fresh-binlog</c> will accept it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, not chosen. On Vixen a complete build's binlog covers <b>98 %</b> of the tree —
    ///     the missing 2 % is one project the solution does not build — and an incremental build's
    ///     covers <b>1 %</b>. Anything in that gap separates the two; 90 leaves room for a repository
    ///     with several projects outside its solution without letting a partial build through.
    /// </remarks>
    internal const int CoverageFloor = 90;

    /// <summary>What share of the selected files the binlog actually covered.</summary>
    internal static int CoveragePercent(int selected, int missing) =>
        selected == 0 ? 100 : (int)Math.Round(100.0 * (selected - missing) / selected, MidpointRounding.AwayFromZero);

    /// <summary>
    ///     The verdict on an incomplete binlog: an error the caller must refuse, or a warning.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A ratio, and only under the flag.</b> The two cases have to be told apart and the
    ///     numbers do it cleanly: a *complete* build of Vixen covers 4 642 of 4 717 files — 98 %,
    ///     the missing 75 living in a project the solution does not build, which no binlog will ever
    ///     cover. An *incremental* build's binlog covers 52 of 4 717. That is 1 %.
    ///     <para>
    ///         Refusing on any gap at all would make <c>--require-fresh-binlog</c> unsatisfiable on a
    ///         repository holding one project outside its solution — the same "gate nobody can turn green"
    ///         mistake that made docs/plan/09's <c>formatting: clean</c> unusable.
    ///     </para>
    /// </remarks>
    internal static SkalaSeverity CoverageSeverity(int selected, int missing, bool requireFresh) =>
        requireFresh && CoveragePercent(selected, missing) < CoverageFloor
            ? SkalaSeverity.Error
            : SkalaSeverity.Warning;

    static void ReportStaleness(
        string binlogPath,
        ImmutableArray<CompilationUnit>.Builder units,
        LoadRequest request,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in units) {
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                known.Add(Path.GetFullPath(tree.FilePath));
            }
        }

        var binlogTime = File.GetLastWriteTimeUtc(binlogPath);
        var newest = DateTime.MinValue;
        var missing = new List<string>();
        var selected = 0;

        // ⚠ Scoped to what was asked for. `skala check Core/` against a binlog that covers `Core/`
        // is complete, whatever else the repository holds, and reporting the rest as missing would
        // make the check unusable on any path-scoped run — which is most of them.
        foreach (var file in Selected(request)) {
            selected++;
            var written = File.GetLastWriteTimeUtc(file);
            if (written > newest) {
                newest = written;
            }

            if (!known.Contains(file) && !IsGenerated(file)) {
                missing.Add(file);
            }
        }

        if (missing.Count > 0) {
            var covered = selected - missing.Count;
            var percent = CoveragePercent(selected, missing.Count);

            diagnostics.Add(
                new SkalaDiagnostic(
                    RuleIds.BinlogMissingFile,
                    CoverageSeverity(selected, missing.Count, request.RequireFreshBinlog),
                    $"the binary log covers {covered.ToString(CultureInfo.InvariantCulture)} of "
                    + $"{selected.ToString(CultureInfo.InvariantCulture)} selected source file(s) "
                    + $"({percent.ToString(CultureInfo.InvariantCulture)} %); "
                    + $"{missing.Count.ToString(CultureInfo.InvariantCulture)} were in no compilation "
                    + "and were not analysed",
                    binlogPath,
                    1,
                    "⚠ An incremental build's binlog holds only the projects MSBuild rebuilt, and it is "
                    + "not stale — a timestamp comparison cannot see this. Rebuild with "
                    + "`--no-incremental`, or re-run without `--load=binlog`."
                )
            );
        }

        // ⚠ The per-file lines stay warnings even under the flag. The summary above is the verdict;
        // twenty error-coloured file names underneath it are noise on a tree that is at 98 %.
        foreach (var file in missing.Take(20)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    RuleIds.BinlogMissingFile,
                    SkalaSeverity.Warning,
                    "the binary log names no compilation containing this file, so it was not analysed; rebuild",
                    file
                )
            );
        }

        if (missing.Count > 20) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    RuleIds.BinlogMissingFile,
                    SkalaSeverity.Warning,
                    $"and {(missing.Count - 20).ToString(CultureInfo.InvariantCulture)} more file(s) are in no compilation; rebuild",
                    request.RepositoryRoot
                )
            );
        }

        if (newest > binlogTime) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    RuleIds.BinlogStaleForFile,
                    request.RequireFreshBinlog ? SkalaSeverity.Error : SkalaSeverity.Info,
                    "the binary log is older than the newest source file; the findings may be about a program that has moved",
                    binlogPath
                )
            );
        }
    }

    /// <summary>
    ///     The source files the run is about: the requested paths, or the whole repository.
    /// </summary>
    /// <remarks>
    ///     ⚠ A path that names a file is itself; a path that names a directory is everything under it.
    ///     A path that is neither contributes nothing rather than throwing — the caller has already
    ///     reported an unresolvable path, and the coverage check is not the place to fail a second time.
    /// </remarks>
    static IEnumerable<string> Selected(LoadRequest request) {
        if (request.Paths.Count == 0) {
            return EnumerateSources(request.RepositoryRoot, request.RepositoryRoot);
        }

        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in request.Paths) {
            var full = Path.GetFullPath(path);

            // ⚠ `.skala/` is filtered here as well as in `EnumerateSources`, because a caller can
            // hand us explicit file paths and one of them does: `arrange` passes the list its own
            // walker collected, and that walker does not exclude `.skala/`. Measured on Vixen —
            // 4 717 source files, and 4 727 "selected" once ten crash reproductions had been
            // written under `.skala/crash/`. A coverage ratio whose denominator counts Skala's own
            // crash evidence is a coverage ratio that drifts every time the formatter trips.
            if (SkalaDirectory.Contains(full)) {
                continue;
            }

            if (File.Exists(full)) {
                if (full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
                    files.Add(full);
                }
            } else if (Directory.Exists(full)) {
                foreach (var file in EnumerateSources(full, request.RepositoryRoot)) {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    /// <summary>
    ///     Every <c>.cs</c> file under <paramref name="root" /> that is source code the repository wants
    ///     looked at.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         This walk decides the denominator of the coverage ratio, so what it counts is what
    ///         <c>--require-fresh-binlog</c> refuses on.
    ///     </b> It used to be a hard-coded list of four
    ///     directory names tested against the <em>absolute</em> path — a second copy of
    ///     <c>FormatCommand.IsExcluded</c>, already disagreeing with it about <c>.claude/</c>, and no
    ///     way at all for a repository to say that a directory holds inputs rather than code. Skala's
    ///     own tree holds 1 924 such files and the ratio read <b>13 %</b> against a complete binlog, so
    ///     every push to <c>master</c> exited 4. Both facts now come from
    ///     <see cref="SourceExclusions" />, which is one list and reads <c>skala.jsonc</c>.
    /// </remarks>
    /// <param name="repositoryRoot">
    ///     What a declared pattern is anchored to. <c>null</c> consults only the built-in directories,
    ///     which is the right answer for a caller that has no repository rather than a reason to skip
    ///     the configuration.
    /// </param>
    internal static IEnumerable<string> EnumerateSources(string root, string? repositoryRoot = null) {
        var exclusions = SourceExclusions.For(repositoryRoot);
        var full = Path.GetFullPath(root);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
            // ⚠ `.expected.cs` is an oracle fixture: the *output* of a conformance run, committed
            // beside its input. Analysing one means analysing the same code twice and reporting
            // findings against a file nobody edits. It stays here rather than moving into
            // `skala.jsonc` because it is a property of the file's name and not of any repository's
            // layout — every tree that runs the conformance harness has them, wherever they live.
            if (file.EndsWith(".expected.cs", StringComparison.Ordinal)) {
                continue;
            }

            // ⚠ Relative to the root being walked, never absolute — see SourceExclusions.Excludes.
            if (exclusions.Excludes(Path.GetRelativePath(full, file), file)) {
                continue;
            }

            yield return file;
        }
    }

    internal static bool IsGenerated(string path) {
        var name = Path.GetFileName(path);
        return name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal
            );
    }

    /// <summary>
    ///     The target framework, read back out of the symbols the build defined.
    /// </summary>
    /// <remarks>
    ///     ⚠ The moniker itself is not on the `csc` command line — MSBuild knows it and the compiler
    ///     does not. What is there is the implicit define the SDK adds, `NET10_0` or `NETSTANDARD2_0`,
    ///     beside the `_OR_GREATER` ladder. Taking the one without the suffix recovers the moniker
    ///     exactly, and returning empty when there is none is honest rather than guessing `net10.0`.
    /// </remarks>
    static string TargetFrameworkOf(CSharpCommandLineArguments parsed) {
        foreach (var symbol in parsed.ParseOptions.PreprocessorSymbolNames) {
            if (symbol.EndsWith("_OR_GREATER", StringComparison.Ordinal)) {
                continue;
            }

            if (symbol.StartsWith("NETSTANDARD", StringComparison.Ordinal)
                || symbol.StartsWith("NETFRAMEWORK", StringComparison.Ordinal)
                || symbol.StartsWith("NETCOREAPP", StringComparison.Ordinal)
                || symbol.Length > 3
                && symbol.StartsWith("NET", StringComparison.Ordinal)
                && char.IsDigit(symbol[3])) {
                return symbol.ToLowerInvariant().Replace('_', '.');
            }
        }

        return string.Empty;
    }

    static string Relative(string root, string path) =>
        path.StartsWith(root, StringComparison.Ordinal)
            ? Path.GetRelativePath(root, path).Replace('\\', '/')
            : path;
}

/// <summary>
///     Splitting a recorded command line the way the compiler's own driver does.
/// </summary>
/// <remarks>
///     ⚠ The binlog records one string, and `csc` arguments contain paths with spaces, quoted response
///     arguments and `/define:"A;B"` values. Splitting on whitespace produces arguments that parse into
///     a compilation missing half its references — which then reports a few hundred CS0246s and looks
///     like the user's code is broken.
/// </remarks>
internal static class CommandLine {
    public static List<string> Split(string line) {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        var any = false;

        foreach (var c in line) {
            switch (c) {
                case '"':
                    quoted = !quoted;
                    any = true;
                    continue;

                case ' ' or '\t' or '\r' or '\n' when !quoted:
                    if (any) {
                        result.Add(current.ToString());
                        current.Clear();
                        any = false;
                    }

                    continue;

                default:
                    current.Append(c);
                    any = true;
                    continue;
            }
        }

        if (any) {
            result.Add(current.ToString());
        }

        return result;
    }
}
