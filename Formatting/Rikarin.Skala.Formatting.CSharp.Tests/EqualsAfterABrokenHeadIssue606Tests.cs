using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a plain member access, behind a head the author broke, stays on the head's last line
///     (#606, Nightly fuzz, case 10828701791419393416).
/// </summary>
/// <remarks>
///     ⚠ #590's name-reading branch asked whether the whole group fits, and a group holding the author's break has
///     an unbounded flat width, which never fits: <c>var (a71, b72</c> / <c>) = source.OrderBy.First.Value;</c>
///     broke its <c>=</c> on a 41-column line. The seed's source broke the chain too, which made the value not a
///     plain member access, so pass one kept the <c>=</c> and pass two, finding the chain joined, broke it.
///     Expected output is the oracle's, measured 2026-10-10 with <c>Testing ask</c>.
/// </remarks>
public sealed class EqualsAfterABrokenHeadIssue606Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Oracle = """
                          class C {
                              void M() {
                                  var (a71, b72
                                      ) = source.OrderBy.First.Value;
                                  var (a71, b72
                                      ) = source.Value;
                                  (int a71, int b72
                                      ) = source.OrderBy.First.Value;
                                  (a71, b72
                                      ) = source.OrderBy.First.Value;
                                  var (a71,
                                      b72) = source.OrderBy.First.Value;
                              }
                          }
                          """;

    [Fact]
    public void AnEqualsBehindABrokenHead_StaysBesideAPlainMember() {
        var formatted = FormatWith(Oracle);
        Assert.Equal(Oracle + "\n", formatted);
    }

    [Fact]
    public void TheSeedsShape_IsIdempotent() {
        const string source = """
                              class C {
                                  void M() {
                                      var (   a71, b72
                              ) = source.OrderBy.
                              First.Value   ;
                                  }
                              }
                              """;
        const string oracle = """
                              class C {
                                  void M() {
                                      var (a71, b72
                                          ) = source.OrderBy.First.Value;
                                  }
                              }
                              """;
        var formatted = FormatWith(source);
        Assert.Equal(oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
