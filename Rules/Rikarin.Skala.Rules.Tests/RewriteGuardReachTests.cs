using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The wider of the two comment guards is asked by exactly the call sites that audited into it.
/// </summary>
/// <remarks>
///     ⚠ <b>This is the half of #325 that matters, and it exists because renaming was not enough.</b>
///     <c>RewriteGuards</c> asks two different questions about the same subject:
///     <c>ContainsCommentOrDirectiveWithinTheEdit</c> reaches exactly the span a fix rewrites, and
///     <c>ContainsCommentOrDirectiveAroundTheDeclaration</c> reaches <c>FullSpan</c> and therefore the
///     comment written ABOVE the node. Picking the wrong one is silent in both directions: the wide
///     question on a narrow edit makes the rule dead on documented code (#302), and the narrow
///     question on a line-deleting fix makes the fix eat the comment — which is worse, because a
///     missed finding is invisible and a destroyed comment is unrecoverable.
///     <para>
///         ⚠
///         <b>
///             Naming them apart stops the mistake being invisible; it does not stop it being
///             made.
///         </b> Both questions were once spelled <c>ContainsCommentOrDirective</c> and told apart
///         only by arity, so copying a guard line from a line-deleting rule into a span-rewriting one
///         compiled and was wrong — which is how the idiom reached four hand-written copies, one of
///         them carrying the doc comment verbatim. Copy-paste is how this spreads, so the wide
///         question is allow-listed: adding a call site fails here and the author has to say, in this
///         file, which shape of fix justifies it.
///     </para>
///     <para>
///         Only two shapes qualify. Either the fix deletes the node's whole <em>line</em> — anything
///         reaching for <c>RewriteGuards.LineSpanOf</c>, or deleting <c>FullSpan</c> outright — so the
///         leading comment is genuinely inside the edit; or the fix deletes the node entirely by its
///         <c>Span</c>, where the comment above is not deleted but <em>orphaned</em> onto whatever
///         member follows. Every entry below names which.
///     </para>
///     <para>
///         ⚠ It reads <c>git ls-files</c> rather than the working tree, and treats an empty listing as
///         a broken instrument: a zero from a check that did not run and a zero from a clean tree are
///         the same zero.
///     </para>
/// </remarks>
public sealed class RewriteGuardReachTests {
    /// <summary>
    ///     Every file permitted to ask the <c>FullSpan</c> question, and the edit that earns it.
    /// </summary>
    static readonly Dictionary<string, string> Permitted = new(StringComparer.Ordinal) {
        ["Rules/Rikarin.Skala.Rules/Modernization/DictionaryLookupAnalyzer.cs"] =
            "SK1033's read-after-ContainsKey shape deletes the following declaration's whole line "
            + "with LineSpanOf. Its OTHER shape rewrites statement.Span only and asks the narrow "
            + "question — both fixtures are committed, so the split is pinned from both sides.",
        ["Rules/Rikarin.Skala.Rules/Modernization/FieldBackedPropertyAnalyzer.cs"] =
            "SK1003 deletes the field by declaration.Span. The doc comment above is not deleted but "
            + "ORPHANED onto the next member, which is why the narrow question is wrong here even "
            + "though the edit is technically inside the node. fixtures/SK1003/negative/comments.cs.",
        ["Rules/Rikarin.Skala.Rules/Modernization/InlineOutVariableAnalyzer.cs"] =
            "SK1054 deletes the standalone declaration's whole line with LineSpanOf.",
        ["Rules/Rikarin.Skala.Rules/Modernization/NullableAnnotationSyntaxAnalyzer.cs"] =
            "SK1094's single-attribute branch deletes list.FullSpan outright, so a #nullable "
            + "directive in the list's leading trivia is inside the edit.",
        ["Rules/Rikarin.Skala.Rules/Modernization/TestAndCastPatternAnalyzer.cs"] =
            "SK1050's as-then-null-check shape deletes the declaration's whole line with LineSpanOf. "
            + "Its four other guards rewrite their own spans and ask the narrow question.",
        ["Rules/Rikarin.Skala.Rules/Modernization/TypePatternAnalyzer.cs"] =
            "SK1015 deletes the cast declaration's whole line with LineSpanOf.",
        ["Rules/Rikarin.Skala.Rules/Cleanup/RedundantPositionalPropertyAnalyzer.cs"] =
            "SK0282 deletes property.FullSpan outright — the whole member, leading trivia included — "
            + "so a documentation comment above it is inside the edit and is a reason to leave the "
            + "declaration alone rather than a thing to delete."
    };

    static readonly Regex Call = new(
        @"RewriteGuards\.ContainsCommentOrDirectiveAroundTheDeclaration\s*\(",
        RegexOptions.Compiled
    );

    static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    [Fact]
    public void OnlyTheAuditedCallSites_AskTheWiderQuestion() {
        var files = TrackedSourceFiles();

        // Anti-vacuity: an empty listing would pass every assertion below.
        Assert.True(files.Count > 100, $"Only {files.Count} tracked C# file(s) were listed.");

        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var relative in files) {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot, relative));
            foreach (Match match in Call.Matches(text)) {
                var lineStart = text.LastIndexOf('\n', match.Index) + 1;
                var prefix = text.Substring(lineStart, match.Index - lineStart);
                if (prefix.Contains("///", StringComparison.Ordinal)
                    || prefix.Contains("//", StringComparison.Ordinal)) {
                    continue;
                }

                found.Add(relative.Replace('\\', '/'));
            }
        }

        // Anti-vacuity again: the guard has known callers, so finding none means the scan is broken
        // rather than that the tree is clean.
        Assert.True(found.Count > 0, "No call site asks the wider question, which cannot be right.");

        var added = found.Where(f => !Permitted.ContainsKey(f)).ToList();
        Assert.True(
            added.Count == 0,
            "⚠ These call sites ask ContainsCommentOrDirectiveAroundTheDeclaration and are not in the "
            + "allow-list:\n  "
            + string.Join("\n  ", added)
            + "\n\nThat question reaches FullSpan, so it sees the comment written ABOVE the node — "
            + "text most fixes never touch. Asking it for a fix that rewrites a span INSIDE the node "
            + "makes the rule silently decline on documented code, which is #302 and is how ten rules "
            + "stayed dead. Use ContainsCommentOrDirectiveWithinTheEdit(tree, theSpanYourFixRewrites) "
            + "unless your fix deletes the node's whole LINE (LineSpanOf / FullSpan) or deletes the "
            + "node outright and would orphan the comment above it. If it does, add the file here "
            + "with the edit that earns it — and a negative fixture where that comment withdraws the "
            + "finding."
        );

        var removed = Permitted.Keys.Where(f => !found.Contains(f)).ToList();
        Assert.True(
            removed.Count == 0,
            "These files are allow-listed and no longer ask the wider question:\n  "
            + string.Join("\n  ", removed)
            + "\n\nIf the guard was correctly narrowed or deleted, drop the entry — a stale allow-list "
            + "reads as audited and is not."
        );
    }

    static List<string> TrackedSourceFiles() {
        var process = Process.Start(
            new ProcessStartInfo("git", "ls-files -- Rules/Rikarin.Skala.Rules/*.cs") {
                WorkingDirectory = RepositoryRoot, RedirectStandardOutput = true
            }
        )!;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToList();
    }
}
