using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using BuildProject = Microsoft.Build.Logging.StructuredLogger.Project;
using SourceText = Microsoft.CodeAnalysis.Text.SourceText;
using Task = Microsoft.Build.Logging.StructuredLogger.Task;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
/// ADR-007's primary path: reconstruct compilations from what the compiler was actually told.
/// </summary>
/// <remarks>
/// <code>
/// dotnet build -bl  →  BinaryLog.ReadBuild()  →  every CscTask.CommandLineArguments
///                   →  CSharpCommandLineParser.Default.Parse(...)
///                   →  sources, references, options, analyzers, editorconfigs
/// </code>
/// This is what the build compiled — generated sources included, conditional symbols correct,
/// analyzer references as configured, multi-targeting expressed as one <c>Csc</c> invocation per
/// target framework. No design-time build, no MSBuild evaluation, no SDK-version sensitivity beyond
/// the one that already produced the binlog. ⚠ It is the only option that is <em>definitionally</em>
/// correct, and it costs one real build, which CI is doing anyway.
/// </remarks>
public static class BinlogLoader {
    /// <summary>Where a binlog is looked for when none is named.</summary>
    public static readonly string[] Conventions = [
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
                    "SK9022",
                    SkalaSeverity.Warning,
                    "no binary log was found; run `dotnet build -bl:artifacts/skala.binlog`",
                    request.RepositoryRoot,
                    0,
                    "Looked for: " + string.Join(", ", Conventions)
                )
            );

            return new LoadedProject { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        // ⚠ Before any MSBuild type is touched in this frame. See MSBuildRuntime's remarks.
        if (!MSBuildRuntime.Ensure(out var locatorError)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    "SK9022",
                    SkalaSeverity.Warning,
                    $"the SDK's MSBuild could not be located, so '{path}' cannot be read: {locatorError}",
                    path
                )
            );

            return new LoadedProject { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        var invocations = new List<(string Project, string Arguments)>();
        if (!TryRead(path, invocations, diagnostics)) {
            return new LoadedProject { Mode = LoadMode.Binlog, Diagnostics = diagnostics.ToImmutable() };
        }

        var units = ImmutableArray.CreateBuilder<CompilationUnit>();
        foreach (var (project, arguments) in invocations) {
            var unit = Build(project, arguments, request, diagnostics, cancellation);
            if (unit is not null) {
                units.Add(unit);
            }
        }

        ReportStaleness(path, units, request, diagnostics);

        return new LoadedProject {
            Mode = LoadMode.Binlog,
            Units = units.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable(),
            Summary =
                $"binlog {Relative(request.RepositoryRoot, path)} ({units.Count.ToString(CultureInfo.InvariantCulture)} compilation(s))"
        };
    }

    /// <summary>
    /// ⚠ Not inlined, so that <see cref="MSBuildRuntime.Ensure"/> has run before the JIT resolves
    /// this frame's references to MSBuild's types.
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
                    "SK9022",
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
    /// Every <c>Csc</c> invocation in the log, with the project that ran it.
    /// </summary>
    /// <remarks>
    /// ⚠ Recursive over <c>Children</c> rather than through the library's visitor, because the
    /// visitor's shape has changed between StructuredLogger versions and a walk of a tree of nodes
    /// is not the part of this worth a dependency on an API surface.
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
            parsed = CSharpCommandLineParser.Default.Parse(arguments, baseDirectory, sdkDirectory: null);
        } catch (ArgumentException exception) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    "SK9022",
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

            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, full, cancellationToken: cancellation));

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

        return new CompilationUnit {
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
    /// ⚠ The two staleness failures, and the second is the dangerous one.
    /// </summary>
    /// <remarks>
    /// A file whose content moved since the build is <c>SK9020</c> — the current text is what is
    /// analysed, so the report is about the working tree. A file that exists and is in no
    /// compilation is <c>SK9021</c> and a warning, because it is <em>silently unanalysed</em>: the
    /// run comes back clean and says nothing about it, which is the worst failure this tool can
    /// have.
    /// </remarks>
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

        foreach (var file in EnumerateSources(request.RepositoryRoot)) {
            var written = File.GetLastWriteTimeUtc(file);
            if (written > newest) {
                newest = written;
            }

            if (!known.Contains(file) && !IsGenerated(file)) {
                missing.Add(file);
            }
        }

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

    internal static IEnumerable<string> EnumerateSources(string root) {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
            var separator = Path.DirectorySeparatorChar;

            // ⚠ `.expected.cs` is an oracle fixture: the *output* of a conformance run, committed
            // beside its input. Analysing one means analysing the same code twice and reporting
            // findings against a file nobody edits.
            if (file.EndsWith(".expected.cs", StringComparison.Ordinal)) {
                continue;
            }

            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                || file.Contains($"{separator}.git{separator}", StringComparison.Ordinal)
                || file.Contains($"{separator}artifacts{separator}", StringComparison.Ordinal)) {
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
    /// The target framework, read back out of the symbols the build defined.
    /// </summary>
    /// <remarks>
    /// ⚠ The moniker itself is not on the `csc` command line — MSBuild knows it and the compiler
    /// does not. What is there is the implicit define the SDK adds, `NET10_0` or `NETSTANDARD2_0`,
    /// beside the `_OR_GREATER` ladder. Taking the one without the suffix recovers the moniker
    /// exactly, and returning empty when there is none is honest rather than guessing `net10.0`.
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
/// Splitting a recorded command line the way the compiler's own driver does.
/// </summary>
/// <remarks>
/// ⚠ The binlog records one string, and `csc` arguments contain paths with spaces, quoted response
/// arguments and `/define:"A;B"` values. Splitting on whitespace produces arguments that parse into
/// a compilation missing half its references — which then reports a few hundred CS0246s and looks
/// like the user's code is broken.
/// </remarks>
internal static class CommandLine {
    public static List<string> Split(string line) {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        var any = false;

        for (var i = 0; i < line.Length; i++) {
            var c = line[i];
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
