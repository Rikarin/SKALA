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

    /// <summary>
    ///     Fuzz 7696270417166025555: the author's break is in front of the block's <c>{</c>, which the brace style
    ///     joins to the arrow just as it joins the block's own gaps. The oracle answers the same for the brace below
    ///     its arrow (at column 0 and indented), for <c>=&gt; {</c> and for the block on one line.
    /// </summary>
    [Theory]
    [InlineData("=>\n{\n            return 97970;\n        }")]
    [InlineData("=>\n        {\n            return 97970;\n        }")]
    [InlineData("=> {\n            return 97970;\n        }")]
    [InlineData("=> { return 97970; }")]
    public void ABraceBelowItsArrow_ReadsAsJoined(string body) {
        var source = """
                     public class C {
                         public long M() {
                             ((char First, ValueTask<byte> Second) First, (Lazy<string> First, string Second) Second) v99 = ((x, y) BODY) ? 1.0m : Emit((true, "ssssss"), Emit("sssssssssssssssssssssssss", x100 => "ssssssssssss", 3_000_000L), Cast<ValueTask<IEnumerable<CancellationToken>>, ImmutableArray<string>>("utf8"u8, 1.5d, 42266, ref null));
                         }
                     }
                     """.Replace("BODY", body, StringComparison.Ordinal);
        var oracle = """
                     public class C {
                         public long M() {
                             ((char First, ValueTask<byte> Second) First, (Lazy<string> First, string Second) Second) v99 =
                                 ((x, y) => { return 97970; })
                                     ? 1.0m
                                     : Emit(
                                         (true, "ssssss"),
                                         Emit("sssssssssssssssssssssssss", x100 => "ssssssssssss", 3_000_000L),
                                         Cast<ValueTask<IEnumerable<CancellationToken>>, ImmutableArray<string>>(
                                             "utf8"u8,
                                             1.5d,
                                             42266,
                                             ref null
                                         )
                                     );
                         }
                     }

                     """;

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     Fuzz 5989313709516589063 (#629): the author's break is in front of a comma in the lambda's parameter list,
    ///     which the formatter always joins to its item. The oracle answers the same as for the list written on one
    ///     line, one break or two.
    /// </summary>
    [Theory]
    [InlineData("(x, y)")]
    [InlineData("(x\n, y)")]
    [InlineData("(x\n    , y)")]
    public void ABreakBeforeACommaInTheParameters_ReadsAsJoined(string parameters) {
        var source = """
                     namespace N {
                         public class C {
                             public void M() {
                                 switch (a) {
                                     case [_, .. var rest52]:
                                         if (c) {
                                             Dictionary<(Task<string> First, Memory<int> Second), List<(Task<string> First, Memory<int> Second)>> v75 = (PARAMETERS => {
                                                 return @"verbatim\path";
                                             }) ? this.Items : (source is > 0 and < 100);
                                         }
                                         break;
                                 }
                             }
                         }
                     }
                     """.Replace("PARAMETERS", parameters, StringComparison.Ordinal);
        var oracle = """
                     namespace N {
                         public class C {
                             public void M() {
                                 switch (a) {
                                     case [_, .. var rest52]:
                                         if (c) {
                                             Dictionary<(Task<string> First, Memory<int> Second),
                                                 List<(Task<string> First, Memory<int> Second)>> v75 =
                                                 ((x, y) => { return @"verbatim\path"; }) ? this.Items : (source is > 0 and < 100);
                                         }

                                         break;
                                 }
                             }
                         }
                     }

                     """;

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }
}
