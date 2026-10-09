using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A lambda's single-statement block the formatter puts back on one line does not turn a conditional's
///     condition away from #553's rule (Nightly <c>fuzz --seed=20261009</c>, case 8573762464065711162).
/// </summary>
/// <remarks>
///     ⚠ Found as an ungoverned-gap failure: <c>items[1 .. ^2]</c> is two columns wider than
///     <c>items[1..^2]</c>, which moved the ordering rule's fitted margin from breaking the <c>=</c> to keeping
///     it. The first pass joined the block anyway; the second, finding it joined, measured the condition and
///     broke the <c>=</c>. Expected outputs are the oracle's, measured 2026-10-09 with <c>Testing ask</c>, for
///     both spellings of the range and for the block written open and on one line.
/// </remarks>
public sealed class JoinedBlockInAConditionNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    [Theory]
    [InlineData("1..^2")]
    [InlineData("1 .. ^2")]
    public void TheMinimisedCase_BreaksTheEquals_AndIsIdempotent(string range) {
        var source = """
                     public sealed partial class T65<T66, T67, T68> {
                       private Dictionary<ImmutableArray<string?>, List< ImmutableArray<string?>>>  f81 = new bool(((x, y) => {
                       return "ssssssssssssssssssssss";
                       })) ? items[RANGE] : typeof(StringBuilder );
                     }
                     """.Replace("RANGE", range, StringComparison.Ordinal);
        var oracle = """
                     public sealed partial class T65<T66, T67, T68> {
                         private Dictionary<ImmutableArray<string?>, List<ImmutableArray<string?>>> f81 =
                             new bool(((x, y) => { return "ssssssssssssssssssssss"; })) ? items[RANGE] : typeof(StringBuilder);
                     }

                     """.Replace("RANGE", range, StringComparison.Ordinal);

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }
}
