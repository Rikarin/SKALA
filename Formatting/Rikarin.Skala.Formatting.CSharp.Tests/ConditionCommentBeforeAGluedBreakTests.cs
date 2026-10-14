using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #625, fuzz 7876387652505925107: a conditional's condition measured flat keeps the block comment in front of a
///     break the formatter reads as nothing, so <c>Fn(zzzz… /* f */</c> / <c>)</c> measures what
///     <c>Fn(zzzz… /* f */)</c> does.
/// </summary>
/// <remarks>
///     ⚠ Pass two lost the comment's eight columns with the break before the <c>)</c>, measured the condition narrower,
///     and broke the <c>=</c> pass one had kept. The expected output is the oracle's for the one-line input and for
///     pass one's output alike, measured 2026-10-14 with <c>Testing ask</c>.
/// </remarks>
public sealed class ConditionCommentBeforeAGluedBreakTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Oracle = """
                          class C {
                              void M1() {
                                  string vvvv /* f */ = Fn(
                                      zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz /* f */
                                  )
                                      ? first
                                      : second;
                              }
                          }

                          """;

    [Fact]
    public void TheOneLineInput_KeepsTheEquals() {
        var first = FormatWith(
            """
            class C {
                void M1() {
                    string vvvv /* f */  = Fn (zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz /* f */ ) ? first : second;
                }
            }
            """
        );
        Assert.Equal(Oracle, first);
        Assert.Equal(first, FormatWith(first));
    }

    [Fact]
    public void PassOnesOutput_IsLeftAlone() => Assert.Equal(Oracle, FormatWith(Oracle));
}
