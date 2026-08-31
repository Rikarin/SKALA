using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
///     The one answer to "is this <c>.cs</c> file source code this repository wants looked at" — the
///     built-in scratch directories, plus whatever <c>skala.jsonc</c>'s <c>"exclude"</c> declares.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This type exists because the question was being answered in four places and each of them
///         was wrong differently.
///     </b> <c>FormatCommand.IsExcluded</c> knew about <c>.claude/</c> and not
///     <c>.skala/</c>; <c>BinlogLoader.EnumerateSources</c> knew about <c>.skala/</c> and not
///     <c>.claude/</c>, and tested the <em>absolute</em> path, which is the bug that made
///     <c>skala format &lt;repo root&gt;</c> rewrite 2 796 files inside another agent's worktree;
///     <c>CorpusSample</c> and <c>ToolDiagnosticIdTests</c> each carry a third and fourth list. Four
///     lists that must agree and are not derived from one another agree until the first one moves.
///     <para>
///         ⚠ <b>And no list of directory names could have answered it anyway.</b> This repository holds
///         <b>1 924</b> <c>.cs</c> files that are deliberately in no compilation — <c>Testing/corpus/</c>,
///         <c>Rules/Rikarin.Skala.Rules.Tests/fixtures/</c> and that project's <c>corpus/</c> — every one
///         of them declared as data by a <c>&lt;Compile Remove&gt;</c> in the owning project. Nothing
///         outside MSBuild could see that, so the coverage ratio behind
///         <c>--require-fresh-binlog</c> counted them in its denominator, read <b>13 %</b> against a
///         binlog that was in fact complete, and failed every CI run on <c>master</c> with exit 4.
///         Naming the good directories on the command line instead is the workaround the <c>Lint</c>
///         target already carries, and it is why a new project under <c>Testing/</c> is invisible to
///         <c>Lint</c> until somebody remembers to add it.
///     </para>
///     <para>
///         So the declaration lives where docs/plan/03 § "What lives in <c>skala.jsonc</c>" always said
///         it lives — in <c>skala.jsonc</c>, which is the file about <em>where to look</em> — and every
///         invocation honours it rather than only the one CI happens to write out. A workflow-only
///         exclusion would have left <c>skala check</c> broken for everybody running it by hand, which
///         is the population the gate has to be adoptable for.
///     </para>
///     <para>
///         ⚠ The globbing is <see cref="SectionMatcher" />'s, which is Roslyn's, which is the compiler's.
///         A second glob dialect in a tool whose entire configuration story is "<c>.editorconfig</c> and
///         nothing else" would be a second thing to learn and a second thing to get subtly wrong; doc 03
///         writes these patterns as <c>"**/obj/**"</c> and <c>"artifacts/**"</c>, which are already
///         editorconfig section names.
///     </para>
/// </remarks>
public sealed class SourceExclusions {
    const string ProbeKey = "skala_exclude_probe";

    /// <summary>
    ///     The directories no configuration can put back, because nothing in them is the user's source.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>.claude/</c> is here because
    ///     <b>
    ///         an agent worktree is a second checkout of this repository
    ///         inside it
    ///     </b> — the repository's own <c>.gitignore</c> says exactly that. Git honours it; a
    ///     walk over <see cref="SearchOption.AllDirectories" /> does not. ⚠ <c>.skala/</c> is here because
    ///     it holds crash reproductions, which are Skala's own evidence and must be kept byte-for-byte.
    /// </remarks>
    public static readonly ImmutableArray<string> BuiltInDirectories =
        ["obj", "bin", ".git", ".claude", "artifacts", SkalaDirectory.Name];

    static readonly ConcurrentDictionary<string, SourceExclusions> Cache = new(StringComparer.Ordinal);

    readonly AnalyzerConfigSet? _probe;

    SourceExclusions(string? repositoryRoot, ImmutableArray<string> patterns) {
        RepositoryRoot = repositoryRoot;
        Patterns = patterns;
        _probe = patterns.IsEmpty || repositoryRoot is null ? null : Probe(repositoryRoot, patterns);
    }

    /// <summary>Nothing declared: the built-in directories and no more.</summary>
    public static SourceExclusions BuiltIn { get; } = new(null, []);

    /// <summary>The root the declared patterns are anchored to, or <c>null</c> when none are.</summary>
    public string? RepositoryRoot { get; }

    /// <summary>What <c>skala.jsonc</c> declared, in file order.</summary>
    public ImmutableArray<string> Patterns { get; }

    /// <summary>
    ///     The exclusions in force for a repository, read from its <c>skala.jsonc</c> and cached.
    /// </summary>
    /// <remarks>
    ///     ⚠ Cached by root rather than re-read per file. This sits inside two whole-tree walks, and the
    ///     one before it was three <c>string.Contains</c> calls — a configuration read per file would be
    ///     a measurable regression on a repository this walk exists to be fast over.
    /// </remarks>
    public static SourceExclusions For(string? repositoryRoot) =>
        repositoryRoot is null ? BuiltIn : Cache.GetOrAdd(Path.GetFullPath(repositoryRoot), Read);

    /// <summary>Forgets what was cached. For tests that write a <c>skala.jsonc</c> and read it back.</summary>
    public static void Forget() => Cache.Clear();

    static SourceExclusions Read(string repositoryRoot) {
        var path = Path.Combine(repositoryRoot, ToolConfiguration.FileName);
        if (!File.Exists(path)) {
            return new SourceExclusions(repositoryRoot, []);
        }

        try {
            var configuration = ToolConfiguration.FromText(path, File.ReadAllText(path));
            return new(repositoryRoot, configuration.Exclude);
        } catch (IOException) {
            // A configuration we cannot read is not a reason to refuse to walk the tree; the config
            // commands report an unreadable skala.jsonc, and this is not the place to fail twice.
            return new SourceExclusions(repositoryRoot, []);
        } catch (UnauthorizedAccessException) {
            return new SourceExclusions(repositoryRoot, []);
        }
    }

    /// <summary>
    ///     True when this file is not source code the repository wants looked at.
    /// </summary>
    /// <param name="relativeToWalkRoot">
    ///     ⚠ The path <b>below the root the caller named</b>, never the absolute one. An agent worktree
    ///     lives at <c>&lt;repo&gt;/.claude/worktrees/&lt;id&gt;/</c>, so every absolute path inside one
    ///     contains <c>.claude</c>: an absolute test refuses to look at anything at all while working in
    ///     a worktree, and refuses it <em>silently</em>, which is worse than the sweep the exclusion is
    ///     for. Naming a path inside an excluded directory still reaches its files, because the exclusion
    ///     is on the walk and not on the file.
    /// </param>
    /// <param name="fullPath">
    ///     The absolute path, which is what the declared patterns are matched against — they are anchored
    ///     to the repository root and mean nothing relative to whatever directory the caller named.
    /// </param>
    public bool Excludes(string relativeToWalkRoot, string fullPath) {
        var separator = Path.DirectorySeparatorChar;
        var path = separator + relativeToWalkRoot;
        foreach (var directory in BuiltInDirectories) {
            if (path.Contains($"{separator}{directory}{separator}", StringComparison.Ordinal)) {
                return true;
            }
        }

        return _probe is not null
            && _probe.GetOptionsForSourcePath(Path.GetFullPath(fullPath)).AnalyzerOptions.ContainsKey(ProbeKey);
    }

    /// <summary>
    ///     One synthesized <c>.editorconfig</c> at the repository root, every declared pattern a section,
    ///     every section carrying the same probe key. A file matching any of them comes back carrying it.
    /// </summary>
    static AnalyzerConfigSet Probe(string repositoryRoot, ImmutableArray<string> patterns) {
        var text = new StringBuilder("root = true\n");
        foreach (var pattern in patterns) {
            text.Append('[').Append(pattern).Append("]\n").Append(ProbeKey).Append(" = 1\n");
        }

        var config = AnalyzerConfig.Parse(
            SourceText.From(text.ToString()),
            Path.Combine(Path.GetFullPath(repositoryRoot), EditorConfigDocument.FileName)
        );

        return AnalyzerConfigSet.Create(new[] { config });
    }

    /// <summary>
    ///     <c>"exclude": ["Testing/corpus/**", …]</c> — the strings, in order.
    /// </summary>
    /// <remarks>
    ///     ⚠ A pattern is refused rather than written into the synthesized config when it carries a line
    ///     break: a section name is one line, and a pattern containing one would silently turn the rest
    ///     of itself into a key. Everything else is handed to the compiler's matcher as written, so a
    ///     pattern that matches nothing is the author's to notice — the same contract an
    ///     <c>.editorconfig</c> section has.
    /// </remarks>
    internal static ImmutableArray<string> ReadPatterns(JsonElement root) {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("exclude", out var exclude)
            || exclude.ValueKind != JsonValueKind.Array) {
            return [];
        }

        var patterns = ImmutableArray.CreateBuilder<string>();
        foreach (var element in exclude.EnumerateArray()) {
            if (element.ValueKind == JsonValueKind.String
                && element.GetString() is { Length: > 0 } pattern
                && !pattern.Contains('\n', StringComparison.Ordinal)
                && !pattern.Contains('\r', StringComparison.Ordinal)) {
                patterns.Add(pattern);
            }
        }

        return patterns.ToImmutable();
    }
}
