using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A block comment in an <c>=</c>'s call breaks the <c>=</c> right after the <c>(</c> and leaves #446's floor
///     standing anywhere else (Nightly <c>fuzz --seed=4242</c>, case 10014018092937601535).
/// </summary>
/// <remarks>
///     ⚠ Two halves of one test were wrong. Every <c>/* */</c> turned the floor away, so a comment after an
///     argument broke the <c>=</c> where the oracle chops the arguments. And <c>/** d */</c> — a documentation
///     comment to Roslyn — was not seen at all, so after the <c>(</c> the floor kept the <c>=</c>, and pass two,
///     finding the arguments chopped, broke it. Expected outputs are the oracle's, measured 2026-10-09 with
///     <c>Testing ask</c>.
/// </remarks>
public sealed class CommentInACallFloorNightlyTests {
    const string Long1 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c( /** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);";

    const string Long2 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(xxxxxxxxxxxxxxxxxxxxxxx, /* d */ yyyyyyyyyyyyyyyyyyyyyyyy);";

    const string Long3 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(xxxxxxxxxxxxxxxxxxxxxxx /* d */, yyyyyyyyyyyyyyyyyyyyyyyy);";

    const string Long4 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(xxxxxxxxxxxxxxxxxxxxxxx /** d */, yyyyyyyyyyyyyyyyyyyyyyyy);";

    const string Long5 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy /** d */);";

    const string Long6 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy /* d */);";

    const string Long7 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(/* d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);";

    const string Long8 = "var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccccccccccccccc"
        + "c(/** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static readonly string Minimised = $$"""
                                         class EqualsBeforeACallFloor {
                                           void M() {
                                           {{Long1}}
                                           {
                                           }
                                           }
                                         }
                                         """;

    static readonly string MinimisedOracle = $$"""
                                               class EqualsBeforeACallFloor {
                                                   void M() {
                                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                           C{{R('c', 19)}}( /** d */ {{R('x', 23)}}, {{R('y', 24)}});
                                                       { }
                                                   }
                                               }
                                               """;

    static readonly string Positions = $$"""
                                         class EqualsBeforeACallFloor {
                                             void M() {
                                                 {{Long2}}
                                                 {{Long3}}
                                                 {{Long4}}
                                                 {{Long5}}
                                                 {{Long6}}
                                                 {{Long7}}
                                                 {{Long8}}
                                             }
                                         }
                                         """;

    static readonly string PositionsOracle = $$"""
                                               class EqualsBeforeACallFloor {
                                                   void M() {
                                                       var {{R('v', 54)}} = Cccccccccccccccccccc(
                                                           xxxxxxxxxxxxxxxxxxxxxxx, /* d */
                                                           yyyyyyyyyyyyyyyyyyyyyyyy
                                                       );
                                                       var {{R('v', 54)}} = Cccccccccccccccccccc(
                                                           xxxxxxxxxxxxxxxxxxxxxxx /* d */,
                                                           yyyyyyyyyyyyyyyyyyyyyyyy
                                                       );
                                                       var {{R('v', 54)}} = Cccccccccccccccccccc(
                                                           xxxxxxxxxxxxxxxxxxxxxxx /** d */,
                                                           yyyyyyyyyyyyyyyyyyyyyyyy
                                                       );
                                                       var {{R('v', 54)}} = Cccccccccccccccccccc(
                                                           xxxxxxxxxxxxxxxxxxxxxxx,
                                                           yyyyyyyyyyyyyyyyyyyyyyyy /** d */
                                                       );
                                                       var {{R('v', 54)}} = Cccccccccccccccccccc(
                                                           xxxxxxxxxxxxxxxxxxxxxxx,
                                                           yyyyyyyyyyyyyyyyyyyyyyyy /* d */
                                                       );
                                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                           C{{R('c', 19)}}( /* d */ {{R('x', 23)}}, {{R('y', 24)}});
                                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                           C{{R('c', 19)}}( /** d */ {{R('x', 23)}}, {{R('y', 24)}});
                                                   }
                                               }
                                               """;

    [Fact]
    public void TheMinimisedCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Equal(MinimisedOracle + "\n", first);
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>A block or documentation comment after an argument, a comma, or the <c>(</c>.</summary>
    [Fact]
    public void ACommentOffTheParen_LeavesTheFloorStanding() {
        var formatted = FormatWith(Positions);
        Assert.Equal(PositionsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
