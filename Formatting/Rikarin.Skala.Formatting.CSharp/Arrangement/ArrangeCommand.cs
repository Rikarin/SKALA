using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>What <c>skala arrange</c> was asked to do.</summary>
public sealed record ArrangeRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>Report, do not write. Exit 2 when there is anything (docs/plan/09 § "Exit codes").</summary>
    public bool Check { get; init; }

    public bool Diff { get; init; }

    public bool Quiet { get; init; }

    /// <summary><c>a:b</c> — character offsets, over a real edit-to-span map.</summary>
    public string? Range { get; init; }

    /// <summary>⚠ Turns on parenthesis removal. docs/plan/06 gates it for the first release.</summary>
    public bool Aggressive { get; init; }

    public IReadOnlyList<string> Include { get; init; } = [];

    public IReadOnlyList<string> Exclude { get; init; } = [];

    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];

    /// <summary>
    ///     The compilations covering <see cref="Paths" />, supplied by the caller.
    /// </summary>
    /// <remarks>
    ///     ⚠ A delegate rather than a project loader, because docs/plan/02's project graph forbids
    ///     <c>Formatting.CSharp</c> from referencing <c>Analysis</c>. The CLI, the daemon and MCP all
    ///     have both and each supplies its own; here the command only needs "the compilations this file
    ///     participates in", which is the whole of what the semantic half depends on.
    ///     <para>
    ///         ⚠ Null is the documented syntactic mode, not a failure:
    ///         <c>skala format --arrange=syntactic</c> and a loose file with no project both arrive here
    ///         with nothing, and get the subset of the catalogue that needs no semantics.
    ///     </para>
    /// </remarks>
    public Func<IReadOnlyList<string>, IReadOnlyList<CSharpCompilation>>? Compilations { get; init; }
}

/// <summary>
///     The implementation behind <c>skala arrange</c> (docs/plan/11 § "Command surface").
/// </summary>
/// <remarks>⚠ The exit codes are <see cref="ExitCodes" />'s; see the note on <see cref="FormatCommand" />.</remarks>
public static class ArrangeCommand {
    public static CommandResult Run(ArrangeRequest request, CancellationToken cancellation = default) {
        var output = new StringBuilder();
        var root = request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".");
        var crashRoot = root is null ? null : Path.Combine(root, ".skala");

        var files = FormatCommand.Collect(request.Paths).ToList();
        if (files.Count == 0) {
            return new CommandResult(0, request.Quiet ? string.Empty : "no C# files\n");
        }

        var compilations = request.Compilations?.Invoke(files) ?? [];
        var filter = ArrangementFilter.Parse(request.Include, request.Exclude);
        var range = ParseRange(request.Range);

        var changed = 0;
        var failures = 0;
        var syntacticOnly = 0;
        var diagnostics = new List<SkalaDiagnostic>();
        var applied = new Dictionary<string, int>(StringComparer.Ordinal);

        // ⚠ Sequential, unlike `format`. Each file's re-bind mutates a compilation, and doc 06 is
        // explicit that this is the trade being made: "arrange is minutes-scale on a large tree and
        // format is seconds-scale. That is the correct trade: whitespace is cheap and constant, tree
        // rewrites are rare and must be right."
        foreach (var file in files) {
            cancellation.ThrowIfCancellationRequested();
            try {
                var text = CSharpFormatter.Read(file);
                var options = ConfigurationCache.Options(EditorConfigChain.For(file), request.Overrides);
                var arrangement = new ArrangementOptions(
                    options,
                    compilations.Count > 0 ? ArrangementScope.Full : ArrangementScope.Syntactic,
                    request.Aggressive
                );

                var owning = Owning(compilations, file);
                if (owning.Count == 0) {
                    syntacticOnly++;
                }

                var result = ArrangementPipeline.Run(
                    file,
                    text,
                    new PhaseOneOptions(options),
                    arrangement,
                    owning.FirstOrDefault(),
                    Removable(owning, file, null, cancellation),
                    // ⚠ The pipeline rewrites the text its removable set was computed for, and this
                    // is the only caller that owns more than one compilation — so it is the only one
                    // that can re-ask the question the way doc 06 requires it to be asked. Left to
                    // recompute from the single compilation it holds, the pipeline would answer for
                    // one target framework. See SK-FUZZ-0018.
                    (rewritten, token) => Removable(owning, file, rewritten, token),
                    crashRoot,
                    request.Define,
                    filter,
                    cancellation
                );

                diagnostics.AddRange(result.Diagnostics);
                var edits = range is { } span ? EditEmitter.Restrict(result.Edits, span) : result.Edits;
                if (edits.Count == 0) {
                    continue;
                }

                changed++;
                foreach (var id in result.Applied) {
                    applied[id] = applied.GetValueOrDefault(id) + 1;
                }

                var final = EditEmitter.Apply(text.ToString(), edits);
                if (request.Diff) {
                    output.Append(UnifiedDiff.Render(Relative(root, file), text.ToString(), final));
                } else if (!request.Quiet) {
                    output.Append(Relative(root, file))
                        .Append("  ")
                        .AppendLine(string.Join(", ", result.Applied.Select(ArrangeIds.NameOf)));
                }

                if (!request.Check && !request.Diff) {
                    File.WriteAllText(file, final, text.Encoding ?? new UTF8Encoding(false));
                }
            } catch (IOException exception) {
                failures++;
                diagnostics.Add(
                    new SkalaDiagnostic(FormatDiagnosticIds.FileIoFailed, SkalaSeverity.Error, exception.Message, file)
                );
            }
        }

        foreach (var diagnostic in diagnostics.Where(static d => d.Severity >= SkalaSeverity.Info)) {
            output.AppendLine(diagnostic.ToString());
            if (diagnostic.Detail is { } detail) {
                output.Append("    ").AppendLine(detail);
            }
        }

        if (!request.Quiet) {
            if (syntacticOnly > 0) {
                // ⚠ Said out loud, every time, and counted per FILE rather than per run. A file
                // that no loaded compilation contains gets the syntactic subset even when other
                // files got the full one, and a syntactic run silently doing a third of the
                // catalogue looks exactly like a full run that found little to do. The difference is
                // a project the caller forgot to load, and it should not take a diff to notice.
                output.Append("⚠ ")
                    .Append(syntacticOnly.ToString(CultureInfo.InvariantCulture))
                    .Append(syntacticOnly == 1 ? " file was" : " files were")
                    .AppendLine(" in no loaded compilation: the syntactic subset only (docs/plan/06).");
            }

            output.Append(changed.ToString(CultureInfo.InvariantCulture))
                .Append(changed == 1 ? " file " : " files ")
                .Append(request.Check || request.Diff ? "would be arranged" : "arranged")
                .Append(", ")
                .Append((files.Count - changed).ToString(CultureInfo.InvariantCulture))
                .AppendLine(" left alone");

            foreach (var (id, count) in applied.OrderByDescending(static pair => pair.Value)) {
                output.Append("  ")
                    .Append(count.ToString(CultureInfo.InvariantCulture).PadLeft(5))
                    .Append("  ")
                    .Append(id)
                    .Append(' ')
                    .AppendLine(ArrangeIds.NameOf(id));
            }
        }

        var exit = failures > 0
            ? ExitCodes.InternalError
            : (request.Check || request.Diff) && changed > 0
                ? ExitCodes.FormattingNeeded
                : ExitCodes.Ok;

        return new CommandResult(exit, output.ToString());
    }

    /// <summary>Every compilation that contains this file.</summary>
    static List<CSharpCompilation> Owning(IReadOnlyList<CSharpCompilation> compilations, string file) {
        var owning = new List<CSharpCompilation>();
        foreach (var compilation in compilations) {
            foreach (var tree in compilation.SyntaxTrees) {
                if (string.Equals(tree.FilePath, file, StringComparison.Ordinal)) {
                    owning.Add(compilation);
                    break;
                }
            }
        }

        return owning;
    }

    /// <summary>
    ///     The usings this file may lose: unused in <b>every</b> compilation it participates in.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/06 § "Usings", and milestone 4's need #6 — the compilation-wide re-bind that
    ///     <c>skala fix</c>'s per-file syntactic check cannot do. A using that looks unused under
    ///     <c>net8.0</c> may be the only source of an extension method under <c>netstandard2.0</c>, or
    ///     may be needed by a <c>#if</c> branch that only one target compiles. The intersection is the
    ///     whole point: with one compilation it is that compilation's answer, and with three it is the
    ///     only answer that is safe under all three.
    ///     <para>
    ///         ⚠ <paramref name="rewritten" /> is the pipeline asking the same question about text it has
    ///         since produced — SK-FUZZ-0018, where the set was computed once against the input and a rule
    ///         then made a directive removable that had not been. Every owning compilation gets the new
    ///         text substituted for its own tree, so the intersection is still an intersection over the
    ///         same file; asking only the first would give a wider answer than any of them agreed to.
    ///     </para>
    /// </remarks>
    static ImmutableHashSet<string> Removable(
        List<CSharpCompilation> owning,
        string file,
        SourceText? rewritten,
        CancellationToken cancellation
    ) {
        ImmutableHashSet<string>? intersection = null;
        foreach (var compilation in owning) {
            foreach (var tree in compilation.SyntaxTrees) {
                if (!string.Equals(tree.FilePath, file, StringComparison.Ordinal)) {
                    continue;
                }

                var current = rewritten is null
                    ? tree
                    : CSharpSyntaxTree.ParseText(rewritten, (CSharpParseOptions)tree.Options, file, cancellation);

                var bound = rewritten is null ? compilation : compilation.ReplaceSyntaxTree(tree, current);
                var unused = UsingsRule.Unused(bound.GetSemanticModel(current), current, cancellation);
                intersection = intersection is null ? unused : intersection.Intersect(unused);
                break;
            }
        }

        return intersection ?? [];
    }

    static SourceSpan? ParseRange(string? range) {
        if (range is not { Length: > 0 }) {
            return null;
        }

        var colon = range.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0
            || !int.TryParse(range[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(range[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end)) {
            return null;
        }

        return SourceSpan.FromBounds(Math.Min(start, end), Math.Max(start, end));
    }

    static string Relative(string? root, string file) =>
        root is null ? file : Path.GetRelativePath(root, file).Replace('\\', '/');
}
