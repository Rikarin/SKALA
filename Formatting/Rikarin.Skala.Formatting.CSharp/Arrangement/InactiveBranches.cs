using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The parses of one file under which each of its inactive preprocessor branches is active (#637).
/// </summary>
/// <remarks>
///     ⚠ A loaded compilation is one configuration. A binlog is the build that ran — Release, say — and
///     a workspace load evaluates Debug, so whichever one arrives, the code under the <em>other</em>
///     <c>#if</c> is <c>DisabledTextTrivia</c>: text the binder never sees. <c>CS8019</c> is then the
///     compiler's honest answer about a configuration that is not the only one the file is built in,
///     and <see cref="UsingsRule.Unused" /> removed <c>using System.Diagnostics;</c> from a file whose
///     only <c>StackTrace</c> sat under <c>#if DEBUG</c>. The safety re-bind cannot see it either: it
///     re-binds under the same symbols, where the rewritten file is fine.
///     <para>
///         ⚠ The intersection across owning compilations was believed to cover this ("a using needed
///         only by a disabled branch … is exactly why the caller intersects"). It covers it only when
///         the other configuration was <em>loaded</em>, which for <c>DEBUG</c> it never is: no loader
///         hands Skala a Debug and a Release compilation of the same project.
///     </para>
///     <para>
///         So the question is asked of the compiler again, under each configuration that activates a
///         branch this parse disabled. The file is re-parsed with the branch's own symbols flipped and
///         bound in place of the original tree; the caller intersects. That keeps the answer the
///         compiler's own opinion — an <c>#if</c> whose code never names the namespace still lets the
///         using go — where a name-matching scan of the disabled text would be Skala's model of binding
///         (extension methods, attribute suffixes, aliases, global usings) and would be wrong in both
///         directions.
///     </para>
///     <para>
///         ⚠ Only the symbols on the branch's own <c>#if</c>/<c>#elif</c> chain are enumerated, not every
///         symbol the file mentions: a Newtonsoft-shaped file names a dozen <c>HAVE_*</c> symbols, and the
///         whole-file product is thousands of parses where the per-branch one is two or four.
///     </para>
/// </remarks>
public static class InactiveBranches {
    /// <summary>
    ///     The most symbols one branch's condition chain may carry before the file is given up on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Past it the answer is "remove nothing from this file", never "ignore the branch": a branch
    ///     that could not be checked is a branch that might need the using.
    /// </remarks>
    public static readonly int MaxSymbolsPerBranch = 8;

    /// <summary>
    ///     One re-parse per configuration needed to make every inactive branch active at least once, or
    ///     null when some branch could not be enumerated and nothing may be removed.
    /// </summary>
    /// <remarks>
    ///     Empty when the file has no inactive branch carrying code, and when every inactive branch is
    ///     dead under every assignment of its symbols — <c>#if false</c>, the <c>#else</c> of
    ///     <c>#if true</c>, a symbol the file itself <c>#undef</c>s. Dead code is not built in any
    ///     configuration, so it cannot need a using.
    /// </remarks>
    public static IReadOnlyList<SyntaxTree>? Variants(SyntaxTree tree, CancellationToken cancellation = default) {
        var root = tree.GetRoot(cancellation);
        if (!root.ContainsDirectives) {
            return [];
        }

        var branches = Branches(root);
        if (branches.Count == 0) {
            return [];
        }

        var options = (CSharpParseOptions)tree.Options;
        var defined = options.PreprocessorSymbolNames.ToHashSet(StringComparer.Ordinal);
        var text = tree.GetText(cancellation);
        var parsed = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
        var variants = new List<SyntaxTree>();

        foreach (var (span, symbols) in branches) {
            cancellation.ThrowIfCancellationRequested();
            if (variants.Exists(variant => Active(variant, span, cancellation))) {
                continue;
            }

            if (symbols.Count > MaxSymbolsPerBranch) {
                return null;
            }

            for (var mask = 0; mask < 1 << symbols.Count; mask++) {
                var assignment = new HashSet<string>(defined, StringComparer.Ordinal);
                for (var bit = 0; bit < symbols.Count; bit++) {
                    if ((mask & (1 << bit)) != 0) {
                        assignment.Add(symbols[bit]);
                    } else {
                        assignment.Remove(symbols[bit]);
                    }
                }

                if (assignment.SetEquals(defined)) {
                    continue;
                }

                var key = string.Join(';', assignment.Order(StringComparer.Ordinal));
                if (!parsed.TryGetValue(key, out var variant)) {
                    variant = CSharpSyntaxTree.ParseText(
                        text,
                        options.WithPreprocessorSymbols(assignment),
                        tree.FilePath,
                        cancellation
                    );
                    parsed[key] = variant;
                }

                if (Active(variant, span, cancellation)) {
                    variants.Add(variant);
                    break;
                }
            }
        }

        return variants;
    }

    /// <summary>
    ///     Every inactive branch that holds code, with the symbols its condition chain reads.
    /// </summary>
    /// <remarks>
    ///     ⚠ A branch's chain is every <c>#if</c> and <c>#elif</c> open at it, including the earlier
    ///     <c>#elif</c>s of its own group, because an <c>#elif</c> is taken only when they are all false.
    ///     Directives inside an inactive region are still parsed as directive trivia, so a nested
    ///     <c>#if</c> under an inactive one is on the stack like any other.
    /// </remarks>
    static List<(TextSpan Span, List<string> Symbols)> Branches(SyntaxNode root) {
        var branches = new List<(TextSpan, List<string>)>();
        var open = new List<List<string>>();

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false)) {
            if (trivia.GetStructure() is DirectiveTriviaSyntax directive) {
                switch (directive) {
                    case IfDirectiveTriviaSyntax opening:
                        open.Add([..Names(opening.Condition)]);
                        break;
                    case ElifDirectiveTriviaSyntax alternative when open.Count > 0:
                        open[^1].AddRange(Names(alternative.Condition));
                        break;
                    case EndIfDirectiveTriviaSyntax when open.Count > 0:
                        open.RemoveAt(open.Count - 1);
                        break;
                }

                continue;
            }

            if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia) || !HoldsCode(trivia.ToString())) {
                continue;
            }

            var symbols = open.SelectMany(static names => names).Distinct(StringComparer.Ordinal).ToList();
            branches.Add((trivia.Span, symbols));
        }

        return branches;
    }

    static IEnumerable<string> Names(ExpressionSyntax condition) =>
        condition.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Select(static name => name.Identifier.ValueText);

    /// <summary>
    ///     Whether disabled text carries anything a using could be needed for: an identifier token.
    /// </summary>
    /// <remarks>
    ///     A branch that holds only comments or blank lines costs a re-parse and a re-bind for an answer
    ///     known in advance.
    /// </remarks>
    static bool HoldsCode(string disabled) {
        foreach (var token in SyntaxFactory.ParseTokens(disabled)) {
            if (token.IsKind(SyntaxKind.IdentifierToken)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the text at <paramref name="span" /> is compiled in <paramref name="variant" />.</summary>
    /// <remarks>
    ///     The source text is the same in every variant, so a span of the original names the same text in
    ///     each, and the directives that bound it are the same lines. An inactive branch is one piece of
    ///     <c>DisabledTextTrivia</c>; active, its first position is a token or ordinary trivia.
    /// </remarks>
    static bool Active(SyntaxTree variant, TextSpan span, CancellationToken cancellation) =>
        !variant.GetRoot(cancellation).FindTrivia(span.Start).IsKind(SyntaxKind.DisabledTextTrivia);
}
