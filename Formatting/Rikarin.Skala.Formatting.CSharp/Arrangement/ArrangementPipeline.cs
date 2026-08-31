using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>What arranging and formatting one document to a fixed point produced.</summary>
public sealed record PipelineResult(
    string Path,
    SourceText Original,
    string Text,
    ImmutableArray<TextEdit> Edits,
    ImmutableArray<string> Applied,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    int Passes,
    bool Converged) {
    public bool Changed => !Edits.IsEmpty;
}

/// <summary>
///     Arrangement and formatting, run to a fixed point.
/// </summary>
/// <remarks>
///     ⚠ This is milestone 4's need #3, and it exists because neither half is a fixed point of the
///     other. Formatting is one document build and one emit and is idempotent on its own; arrangement
///     moves text and the result has to be re-formatted; and re-formatting can expose a *new*
///     arrangement — a block body that was three statements becomes one after a redundant block is
///     lifted out of it, and only then is it a body-style candidate. So the property that has to hold is
///     about the pair:
///     <code>     pipeline(pipeline(x)) == pipeline(x)</code>
///     and it is asserted over every corpus file under both symbol sets, not reasoned about.
///     <para>
///         ⚠ <see cref="MaxPasses" /> is the bound. It is not a safety net that is never reached and quietly
///         papers over an oscillation: reaching it sets <see cref="PipelineResult.Converged" /> false and
///         reports <c>SK9097</c>, and the conformance suite fails on any corpus file that does not converge.
///         Measured over <c>corpus/real/</c>, the observed maximum is 2 — one pass to arrange and format,
///         one to prove nothing more wants to change.
///     </para>
///     <para>
///         ⚠ The removable-usings set follows the text, and that is SK-FUZZ-0018. "Which usings may this
///         file lose" is a question about a <em>text</em>, and every pass rewrites the text it was
///         answered for: hoisting <c>using System;</c> out of a file-scoped namespace makes it duplicate a
///         <c>global using</c> and therefore <c>CS8019</c>-redundant, and rewriting
///         <c>String.Empty</c> to <c>""</c> makes it unused outright — neither has a namespace boundary in
///         common with the other, so this is a property of the set's <em>timing</em> and not of any one
///         rule. Held once, the set is an answer about the input, and the caller who feeds this method its
///         own output gets a removal the first run did not make. So it is recomputed on every pass whose
///         arrangement actually rewrote the tree; see <see cref="Recompute" />.
///     </para>
/// </remarks>
public static class ArrangementPipeline {
    /// <summary>
    ///     ⚠ Four, and the number is a decision. Two is what convergence costs; a third pass means a
    ///     rule and the formatter disagree about something; a fourth is the margin that turns a
    ///     two-cycle into a reported failure rather than an infinite loop. Higher would hide the bug.
    /// </summary>
    public const int MaxPasses = 4;

    /// <summary>⚠ The document arranged and re-formatted until neither half wants to move.</summary>
    public const string DidNotConverge = "SK9097";

    /// <param name="removableUsings">
    ///     The usings the file may lose, answered for <paramref name="text" /> as it arrives.
    /// </param>
    /// <param name="removableFor">
    ///     The same question, re-asked for text this method has rewritten.
    /// </param>
    /// <remarks>
    ///     ⚠ <paramref name="removableFor" /> is how a caller with <b>more than one</b> owning compilation
    ///     keeps the intersection doc 06 § "Usings" requires — "unused in <em>every</em> compilation this
    ///     file participates in". This method holds one compilation, so recomputing from it alone would
    ///     answer for one target framework and could delete a using that another one needs, which is the
    ///     failure the intersection exists to prevent. A caller that owns several compilations
    ///     <b>must</b> supply this; the fallback below is the single-compilation answer, which is exactly
    ///     right for a caller that had one compilation to begin with and is what
    ///     <paramref name="removableUsings" /> already was for such a caller.
    /// </remarks>
    public static PipelineResult Run(
        string path,
        SourceText text,
        in PhaseOneOptions formatting,
        in ArrangementOptions arrangement,
        CSharpCompilation? compilation = null,
        ImmutableHashSet<string>? removableUsings = null,
        Func<SourceText, CancellationToken, ImmutableHashSet<string>>? removableFor = null,
        string? crashRoot = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        ArrangementFilter? filter = null,
        CancellationToken cancellation = default
    ) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var applied = ImmutableArray.CreateBuilder<string>();
        var current = text;
        var currentCompilation = compilation;
        var currentRemovable = removableUsings;
        var converged = false;
        var passes = 0;

        for (; passes < MaxPasses; passes++) {
            cancellation.ThrowIfCancellationRequested();

            var arranged = Arranger.Arrange(
                path,
                current,
                arrangement,
                currentCompilation,
                currentRemovable,
                crashRoot,
                filter,
                cancellation
            );

            foreach (var diagnostic in arranged.Diagnostics) {
                diagnostics.Add(diagnostic);
            }

            if (arranged.Outcome is ArrangementOutcome.NotParseable or ArrangementOutcome.Generated) {
                return new PipelineResult(
                    path,
                    text,
                    text.ToString(),
                    [],
                    [],
                    diagnostics.ToImmutable(),
                    passes + 1,
                    true
                );
            }

            var afterArrange = arranged.Outcome == ArrangementOutcome.Arranged
                ? SourceText.From(arranged.Text, text.Encoding ?? System.Text.Encoding.UTF8)
                : current;

            foreach (var id in arranged.Applied) {
                if (!applied.Contains(id)) {
                    applied.Add(id);
                }
            }

            var formatted = CSharpFormatter.Format(path, afterArrange, formatting, crashRoot, preprocessorSymbols);
            foreach (var diagnostic in formatted.Diagnostics) {
                diagnostics.Add(diagnostic);
            }

            var next = SourceText.From(formatted.Formatted, text.Encoding ?? System.Text.Encoding.UTF8);

            // ⚠ The fixed point is on the *text*, not on "did any rule fire". A rule that fires and
            // produces byte-identical output has not moved anything, and a rule that fires on every
            // pass without changing the text is the loop this bound exists to stop.
            if (next.ContentEquals(current)) {
                converged = true;
                passes++;
                break;
            }

            current = next;

            // ⚠ The compilation has to follow the text. Pass 2 arranges a document the compilation
            // has never seen, and a semantic model over a stale tree answers about the old one — the
            // rules would then read the model for text that no longer exists. Rebuilding it here is
            // most of what the second pass costs, and skipping it is a correctness bug rather than a
            // slow path.
            currentCompilation = Rebind(currentCompilation, path, current, cancellation);

            // ⚠ And so does the removable set, for the same reason and one pass later — SK-FUZZ-0018.
            // Gated on the *arrangement* having fired: a pass in which only the formatter moved
            // something has changed whitespace, and whether a using is unused is a question about
            // binding, which whitespace does not enter. The key stopped carrying the author's spacing
            // in SK-FUZZ-0013's fix, so that is now true of the key as well as of the diagnostic.
            if (arranged.Outcome == ArrangementOutcome.Arranged) {
                currentRemovable = Recompute(
                    removableFor,
                    currentCompilation,
                    path,
                    current,
                    currentRemovable,
                    cancellation
                );
            }
        }

        if (!converged) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    DidNotConverge,
                    SkalaSeverity.Error,
                    $"arrange-and-format did not reach a fixed point in {MaxPasses.ToString(CultureInfo.InvariantCulture)} passes; the file was left untouched",
                    path,
                    0,
                    "A rule and the formatter disagree about this file. This is a Skala bug."
                )
            );

            return new PipelineResult(path, text, text.ToString(), [], [], diagnostics.ToImmutable(), passes, false);
        }

        // ⚠ A real diff, not EditEmitter over an anchor-less layout — see ArrangementEdits.
        var edits = ArrangementEdits.Diff(text.ToString(), current.ToString());
        return new PipelineResult(
            path,
            text,
            current.ToString(),
            [.. edits],
            applied.ToImmutable(),
            diagnostics.ToImmutable(),
            passes,
            true
        );
    }

    /// <summary>
    ///     The usings the file may lose, asked again about the text this pass produced.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="compilation" /> is the <em>rebound</em> one, so its tree for
    ///     <paramref name="path" /> already carries <paramref name="text" /> — the model below is asked
    ///     about the file that exists now rather than the one that arrived. Getting that order wrong
    ///     would replace one stale answer with another.
    ///     <para>
    ///         ⚠ Falling back to <paramref name="previous" /> rather than to the empty set when there is no
    ///         compilation. Empty means "remove nothing" and would be safe, but it would also silently
    ///         un-authorise a removal the caller had already authorised for the file's own text; the
    ///         syntactic scope arrives here with an empty set anyway, so the two agree where it matters and
    ///         this one does not lose the semantic caller's answer if a rebind ever hands back null.
    ///     </para>
    /// </remarks>
    static ImmutableHashSet<string>? Recompute(
        Func<SourceText, CancellationToken, ImmutableHashSet<string>>? removableFor,
        CSharpCompilation? compilation,
        string path,
        SourceText text,
        ImmutableHashSet<string>? previous,
        CancellationToken cancellation
    ) {
        if (removableFor is not null) {
            return removableFor(text, cancellation);
        }

        if (compilation is null) {
            return previous;
        }

        foreach (var tree in compilation.SyntaxTrees) {
            if (string.Equals(tree.FilePath, path, StringComparison.Ordinal)) {
                return UsingsRule.Unused(compilation.GetSemanticModel(tree), tree, cancellation);
            }
        }

        return previous;
    }

    static CSharpCompilation? Rebind(
        CSharpCompilation? compilation,
        string path,
        SourceText text,
        CancellationToken cancellation
    ) {
        if (compilation is null) {
            return null;
        }

        foreach (var tree in compilation.SyntaxTrees) {
            if (!string.Equals(tree.FilePath, path, StringComparison.Ordinal)) {
                continue;
            }

            return compilation.ReplaceSyntaxTree(
                tree,
                CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)tree.Options, path, cancellation)
            );
        }

        return compilation;
    }
}
