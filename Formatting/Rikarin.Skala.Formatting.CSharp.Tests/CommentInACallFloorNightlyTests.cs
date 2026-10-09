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
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = """
                             class EqualsBeforeACallFloor {
                               void M() {
                               var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc( /** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                               {
                               }
                               }
                             }
                             """;

    const string MinimisedOracle = """
                                   class EqualsBeforeACallFloor {
                                       void M() {
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                               Cccccccccccccccccccc( /** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                                           { }
                                       }
                                   }
                                   """;

    const string Positions = """
                             class EqualsBeforeACallFloor {
                                 void M() {
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx, /* d */ yyyyyyyyyyyyyyyyyyyyyyyy);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx /* d */, yyyyyyyyyyyyyyyyyyyyyyyy);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx /** d */, yyyyyyyyyyyyyyyyyyyyyyyy);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy /** d */);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy /* d */);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(/* d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                                     var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(/** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                                 }
                             }
                             """;

    const string PositionsOracle = """
                                   class EqualsBeforeACallFloor {
                                       void M() {
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(
                                               xxxxxxxxxxxxxxxxxxxxxxx, /* d */
                                               yyyyyyyyyyyyyyyyyyyyyyyy
                                           );
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(
                                               xxxxxxxxxxxxxxxxxxxxxxx /* d */,
                                               yyyyyyyyyyyyyyyyyyyyyyyy
                                           );
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(
                                               xxxxxxxxxxxxxxxxxxxxxxx /** d */,
                                               yyyyyyyyyyyyyyyyyyyyyyyy
                                           );
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(
                                               xxxxxxxxxxxxxxxxxxxxxxx,
                                               yyyyyyyyyyyyyyyyyyyyyyyy /** d */
                                           );
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(
                                               xxxxxxxxxxxxxxxxxxxxxxx,
                                               yyyyyyyyyyyyyyyyyyyyyyyy /* d */
                                           );
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                               Cccccccccccccccccccc( /* d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                                           var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                               Cccccccccccccccccccc( /** d */ xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
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
