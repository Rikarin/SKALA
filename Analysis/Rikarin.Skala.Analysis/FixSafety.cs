using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis;

/// <summary>How a rewritten file was checked before it was written.</summary>
public enum FixCheck {
    /// <summary>Re-bound in the compilation the finding came out of.</summary>
    Semantic,

    /// <summary>Re-parsed only: there was no compilation holding this file.</summary>
    Syntactic
}

/// <summary>What the check said about one rewritten file.</summary>
/// <param name="Check">Which of the two checks actually ran.</param>
/// <param name="Introduced">Compiler errors the rewritten text has that the original did not.</param>
/// <param name="Threw">Set when the check could not answer; that is a revert, not a pass.</param>
public readonly record struct FixVerdict(FixCheck Check, ImmutableArray<string> Introduced, string? Threw);

/// <summary>
///     The verification behind <c>skala fix</c>'s revert-on-regression.
/// </summary>
/// <remarks>
///     ⚠ This used to be <c>CSharpSyntaxTree.ParseText(text).GetDiagnostics()</c>, under a comment
///     claiming it caught "a parse or bind error" (#344). <b>It could not.</b>
///     <c>SyntaxTree.GetDiagnostics</c> returns syntactic diagnostics only, and there was no
///     compilation, no reference set and no semantic model anywhere on that path — so it could not
///     return a bind error for any fix, ever. One <c>skala fix --safe</c> run over a ~750-file green
///     tree applied 26 fixes, produced 12 CS1620 and 4 CS0234, reverted nothing and exited 0.
///     <para>
///         ⚠ The rewritten document is bound with <see cref="Compilation.ReplaceSyntaxTree" /> on the
///         compilation the findings already came out of, exactly as
///         <c>ArrangementSafety</c> does. A fresh compilation per file is what makes a re-bind
///         unaffordable; replacing one tree in a compilation that is already built is a single
///         document bind, and the load it needs is the one <c>check</c> already paid for.
///     </para>
///     <para>
///         ⚠ <b>Every</b> compilation holding the file, not the first one. A file in a multi-targeted
///         project is in one compilation per framework, and #343 is precisely a fix that binds under
///         one target framework and not the other (<c>System.Threading.Lock</c> ⇒ CS0234). Checking one
///         of them is checking the wrong half at random. Verified on #343's own reproduction
///         (<c>netstandard2.1;net10.0</c>): the <c>net10.0</c> unit binds the rewrite cleanly and the
///         <c>netstandard2.1</c> one reports CS0234, so the file is reverted.
///     </para>
///     <para>
///         ⚠ <b>An unrestored target framework can therefore cause a false revert</b>, and that is the
///         direction to fail in. The same project with no <c>obj/</c> gives the <c>netstandard2.1</c>
///         unit no references at all, so the rewrite reports CS0400 rather than CS0234 and the file is
///         still reverted — for the wrong reason, but reverted. The old check applied the broken edit
///         instead. A revert costs the finding; a write costs the build. ⚠ It is also why #343's
///         reproduction is not a committed fixture: the diagnostic it produces depends on whether the
///         scratch project was restored, so a test asserting the id would be measuring the test
///         machine.
///     </para>
/// </remarks>
public sealed class FixSafety {
    readonly Dictionary<string, List<Bound>> bound;

    FixSafety(Dictionary<string, List<Bound>> bound) => this.bound = bound;

    /// <summary>Nothing is re-bindable; every file falls back to the parse check.</summary>
    public static FixSafety None { get; } = new(new(StringComparer.Ordinal));

    /// <summary>
    ///     Indexes the loaded compilations by the file each one holds.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="LoadMode.Loose" /> is deliberately excluded rather than used. A loose
    ///     compilation references the running framework and nothing else, so a project's own types are
    ///     unresolved in it and its diagnostics are mostly CS0246 about code that compiles perfectly
    ///     well; binding against it would answer a question about a program that does not exist.
    ///     <c>ArrangementFindings</c> refuses it for the same reason and says so in the same words. The
    ///     rules that need semantics do not run in loose mode either, so what is left to verify there is
    ///     the syntactic fix — which is the class the parse check actually covers.
    /// </remarks>
    public static FixSafety For(LoadedProject? loaded) {
        if (loaded is null || loaded.Mode == LoadMode.Loose || loaded.Units.IsEmpty) {
            return None;
        }

        var map = new Dictionary<string, List<Bound>>(
            SarifWriter.PathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal
        );

        foreach (var unit in loaded.Units) {
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                if (tree.FilePath is not { Length: > 0 } path) {
                    continue;
                }

                string full;
                try {
                    full = Path.GetFullPath(path);
                } catch (ArgumentException) {
                    continue;
                }

                if (!map.TryGetValue(full, out var list)) {
                    list = [];
                    map[full] = list;
                }

                list.Add(new Bound(unit.Compilation, tree));
            }
        }

        return new(map);
    }

    /// <summary>
    ///     The compiler errors <paramref name="rewritten" /> has that <paramref name="original" /> did
    ///     not.
    /// </summary>
    /// <remarks>
    ///     ⚠ A *new* diagnostic, not a larger count — the shipping check compared
    ///     <c>after.Length &gt; before.Length</c>, which passes a rewrite that removed one error and
    ///     introduced a different one. It is also why the signature is id plus message rather than id
    ///     plus offset: an edit moves text, so an offset-keyed set reports every surviving diagnostic as
    ///     both removed and added.
    ///     <para>
    ///         ⚠ Errors only, where <c>ArrangementSafety</c> takes warnings too. <c>fix</c> applies edits
    ///         a rule proposed at a span, and several of those deliberately change shape enough to move a
    ///         nullable or obsolete warning; reverting on that would refuse correct fixes. The failure
    ///         class #344 is about — an overload that no longer applies, a type that is not there under
    ///         this target framework, a name that now collides — is an error every time.
    ///     </para>
    ///     <para>
    ///         ⚠ A throw is not a pass. Binding is where Roslyn's own defects live (SK-FUZZ-0012 takes
    ///         the binder down from <c>GetSymbolInfo</c>), and a safety question that went unanswered is
    ///         a revert, not a permission.
    ///     </para>
    /// </remarks>
    public FixVerdict Verify(
        string path,
        string original,
        string rewritten,
        CancellationToken cancellation = default
    ) {
        string full;
        try {
            full = Path.GetFullPath(path);
        } catch (ArgumentException) {
            full = path;
        }

        if (!bound.TryGetValue(full, out var targets)) {
            return new(FixCheck.Syntactic, Parsed(path, original, rewritten, cancellation), null);
        }

        try {
            return new(FixCheck.Semantic, Rebound(path, original, rewritten, targets, cancellation), null);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            return new(FixCheck.Semantic, [], $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    static ImmutableArray<string> Rebound(
        string path,
        string original,
        string rewritten,
        List<Bound> targets,
        CancellationToken cancellation
    ) {
        var appeared = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var (compilation, tree) in targets) {
            cancellation.ThrowIfCancellationRequested();
            var options = (CSharpParseOptions)tree.Options;

            // ⚠ The compilation's tree is what was on disk when the load ran, and `fix` can have
            // written the file since — the IDE1006 pass does exactly that before the ordinary edits
            // are applied. Where they differ, the baseline is a parse of what this edit was actually
            // made against; comparing against a stale tree would attribute an earlier pass's
            // diagnostics to this one.
            var baseline = tree;
            var host = compilation;
            if (!string.Equals(tree.GetText(cancellation).ToString(), original, StringComparison.Ordinal)) {
                baseline = CSharpSyntaxTree.ParseText(SourceText.From(original), options, path, cancellation);
                host = compilation.ReplaceSyntaxTree(tree, baseline);
            }

            var edited = CSharpSyntaxTree.ParseText(SourceText.From(rewritten), options, path, cancellation);
            var before = Signature(host.GetSemanticModel(baseline).GetDiagnostics(null, cancellation));
            var after = host.ReplaceSyntaxTree(baseline, edited);
            var now = Signature(after.GetSemanticModel(edited).GetDiagnostics(null, cancellation));
            appeared.UnionWith(now.Except(before, StringComparer.Ordinal));
        }

        return [.. appeared.ToImmutable().Order(StringComparer.Ordinal)];
    }

    /// <summary>
    ///     The file's own syntactic errors, for a file no loaded compilation holds.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the whole of the old check and it is kept only as the degraded case, which
    ///     <c>FixCommand</c> names in its output rather than letting it pass for the other one.
    /// </remarks>
    static ImmutableArray<string> Parsed(
        string path,
        string original,
        string rewritten,
        CancellationToken cancellation
    ) {
        var before = Signature(Parse(path, original, cancellation).GetDiagnostics(cancellation));
        var after = Signature(Parse(path, rewritten, cancellation).GetDiagnostics(cancellation));
        return [.. after.Except(before, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    static SyntaxTree Parse(string path, string text, CancellationToken cancellation) =>
        CSharpSyntaxTree.ParseText(SourceText.From(text), CSharpFormatter.ParseOptions, path, cancellation);

    static ImmutableHashSet<string> Signature(IEnumerable<Diagnostic> diagnostics) {
        var set = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Severity == DiagnosticSeverity.Error) {
                set.Add(diagnostic.Id + ": " + diagnostic.GetMessage(CultureInfo.InvariantCulture));
            }
        }

        return set.ToImmutable();
    }

    readonly record struct Bound(CSharpCompilation Compilation, SyntaxTree Tree);
}
