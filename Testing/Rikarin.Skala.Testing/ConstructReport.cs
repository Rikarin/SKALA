using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Testing;

/// <summary>One construct's share of the corpus and of the residue.</summary>
/// <param name="Kind">The syntax kind, as Roslyn names it.</param>
/// <param name="Occurrences">How many times it appears in the corpus.</param>
/// <param name="Lines">How many lines of the oracle's output it is the innermost owner of.</param>
/// <param name="Divergent">How many of those lines Skala did not reproduce.</param>
public sealed record ConstructShare(string Kind, int Occurrences, int Lines, int Divergent) {
    public double Fidelity => Lines == 0 ? 1 : 1 - (double)Divergent / Lines;
}

/// <summary>
///     The rule docs/plan/16 § R1 states as a hard one:
///     <b>
///         any construct appearing in the corpus more
///         than 50 times must be at 100 %; the tail is only allowed in genuinely rare constructs
///     </b>.
/// </summary>
/// <remarks>
///     ⚠ A single fidelity number cannot answer that question, and the divergence classes cannot
///     either: "wrapping: one side continues where the other broke" says what the difference looked
///     like, not what construct it happened in. This attributes every divergent line to the innermost
///     syntax node that owns it, and puts that beside how often the construct occurs — so that a rule
///     about frequency can be checked against frequency.
///     <para>
///         ⚠ The attribution is done against the <em>oracle's</em> output rather than the input, because a
///         divergent line is a line of output and the oracle's is the one that is correct by definition. It
///         parses: the oracle emits compilable C# or the fixture would not have been committed.
///     </para>
///     <para>
///         ⚠ Measured with the oracle's own preprocessor symbols supplied. Without them a file wrapped in a
///         <c>#if</c> is disabled text for Skala and reproduced unchanged, and every line of it counts
///         against whatever construct happens to own it — which attributes SK-DIV-0004 to
///         <c>ClassDeclaration</c> and <c>Block</c> and says nothing about either.
///     </para>
/// </remarks>
public static class ConstructReport {
    public static IReadOnlyList<ConstructShare> Build(string set, IReadOnlyList<string>? symbols = null) {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var divergent = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Corpus.Files(set).Where(static f => f.HasFixture)) {
            var expected = TextNormalisation.Normalise(OracleFixture.Read(file));
            var actual = TextNormalisation.Normalise(
                Formatting.CSharp.CSharpFormatter.Format(
                    file.Path,
                    Formatting.CSharp.CSharpFormatter.Read(file.Path),
                    Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(file.Path).Options,
                    null,
                    symbols ?? []
                ).Formatted
            );

            var text = SourceText.From(expected);
            var root = CSharpSyntaxTree.ParseText(text, Formatting.CSharp.CSharpFormatter.ParseOptions).GetRoot();

            foreach (var node in root.DescendantNodesAndSelf()) {
                Bump(occurrences, node.Kind().ToString());
            }

            var owner = OwnersByLine(text, root);
            for (var i = 0; i < owner.Length; i++) {
                Bump(lines, owner[i]);
            }

            foreach (var index in DivergentLines(expected, actual)) {
                if (index < owner.Length) {
                    Bump(divergent, owner[index]);
                }
            }
        }

        var kinds = new HashSet<string>(lines.Keys, StringComparer.Ordinal);
        kinds.UnionWith(divergent.Keys);
        return [
            .. kinds
                .Select(kind => new ConstructShare(
                        kind,
                        occurrences.GetValueOrDefault(kind),
                        lines.GetValueOrDefault(kind),
                        divergent.GetValueOrDefault(kind)
                    )
                )
                .OrderByDescending(static share => share.Divergent)
                .ThenBy(static share => share.Kind, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     Where the divergent lines attributed to one construct actually are.
    /// </summary>
    /// <remarks>
    ///     ⚠ The report ranks by line count and R1 counts <em>constructs</em>, so the work queue the two
    ///     imply is not the same one: a construct with two divergent lines is as far from the rule as
    ///     one with ninety. This is how the two-line ones get found.
    /// </remarks>
    public static string Locate(string set, string kind, IReadOnlyList<string>? symbols = null) {
        var builder = new StringBuilder();
        foreach (var file in Corpus.Files(set).Where(static f => f.HasFixture)) {
            var expected = TextNormalisation.Normalise(OracleFixture.Read(file));
            var actual = TextNormalisation.Normalise(
                Formatting.CSharp.CSharpFormatter.Format(
                    file.Path,
                    Formatting.CSharp.CSharpFormatter.Read(file.Path),
                    Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(file.Path).Options,
                    null,
                    symbols ?? []
                ).Formatted
            );

            if (string.Equals(expected, actual, StringComparison.Ordinal)) {
                continue;
            }

            var text = SourceText.From(expected);
            var root = CSharpSyntaxTree.ParseText(text, Formatting.CSharp.CSharpFormatter.ParseOptions).GetRoot();
            var owner = OwnersByLine(text, root);
            var lines = TextNormalisation.Lines(expected);
            foreach (var index in DivergentLines(expected, actual)) {
                if (index < owner.Length && string.Equals(owner[index], kind, StringComparison.Ordinal)) {
                    builder.Append(file.ToString())
                        .Append(':')
                        .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                        .Append("  ")
                        .AppendLine(index < lines.Length ? lines[index] : string.Empty);
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>The innermost node that owns each line, by kind.</summary>
    static string[] OwnersByLine(SourceText text, SyntaxNode root) {
        var owner = new string[text.Lines.Count];
        Array.Fill(owner, "(file)");

        // ⚠ Outer nodes first, inner ones overwriting: the innermost node that touches a line is the
        // one that owns it. A whole file is a CompilationUnit and attributing its 900 lines to that
        // would make every number meaningless.
        foreach (var node in root.DescendantNodesAndSelf()) {
            var span = text.Lines.GetLinePositionSpan(node.Span);
            for (var line = span.Start.Line; line <= span.End.Line && line < owner.Length; line++) {
                owner[line] = node.Kind().ToString();
            }
        }

        return owner;
    }

    /// <summary>The indices of the oracle's lines that Skala did not reproduce.</summary>
    static IEnumerable<int> DivergentLines(string expected, string actual) {
        var left = TextNormalisation.Lines(expected);
        var right = TextNormalisation.Lines(actual);
        var index = 0;
        foreach (var entry in LineDiff.Compute(left, right)) {
            switch (entry.Kind) {
                case LineDiff.Kind.Same:
                    index++;
                    break;

                case LineDiff.Kind.Removed:
                    yield return index++;
                    break;

                default:
                    // An added line is Skala's own; it is charged to the oracle line it displaced,
                    // which the Removed arm has already counted.
                    break;
            }
        }
    }

    static void Bump(Dictionary<string, int> counter, string key) => counter[key] = counter.GetValueOrDefault(key) + 1;

    /// <summary>
    ///     The report, and the R1 verdict.
    /// </summary>
    /// <param name="threshold">
    ///     docs/plan/16 § R1's "more than 50 times". A construct at or below it is allowed a tail.
    /// </param>
    public static string Render(IReadOnlyList<ConstructShare> shares, int threshold = 50) {
        var builder = new StringBuilder();
        var common = shares.Where(share => share.Occurrences > threshold).ToArray();
        var failing = common.Where(static share => share.Divergent > 0).ToArray();

        builder.Append("R1: constructs occurring more than ")
            .Append(threshold.ToString(CultureInfo.InvariantCulture))
            .Append(" times: ")
            .Append(common.Length.ToString(CultureInfo.InvariantCulture))
            .Append("; at 100 %: ")
            .Append((common.Length - failing.Length).ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        builder.AppendLine();
        builder.AppendLine("kind                                     occurrences   lines  divergent  fidelity");
        foreach (var share in shares.Where(static s => s.Divergent > 0).Take(30)) {
            builder.Append(share.Kind.PadRight(40))
                .Append(share.Occurrences.ToString(CultureInfo.InvariantCulture).PadLeft(12))
                .Append(share.Lines.ToString(CultureInfo.InvariantCulture).PadLeft(8))
                .Append(share.Divergent.ToString(CultureInfo.InvariantCulture).PadLeft(11))
                .Append((share.Fidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(10))
                .AppendLine();
        }

        return builder.ToString();
    }
}
