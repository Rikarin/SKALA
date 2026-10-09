using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A block comment between a held value's <c>)</c> and its <c>;</c> counts toward the value's line (Nightly
///     <c>fuzz --seed=20261009</c>, case 10944625209729174497).
/// </summary>
/// <remarks>
///     ⚠ #528's held-value width counted the <c>;</c> as one column and not the comment in front of it, so the
///     table kept an <c>=</c> the comment had pushed past the margin and chopped the arguments; pass two, with
///     them chopped, broke the <c>=</c>. The <c>var</c> rows are the oracle's, measured 2026-10-09 with
///     <c>Testing ask</c>. ⚠ For a typed local the oracle breaks between the type and the name instead — and
///     breaks the <c>=</c> for the same line without the comment — so that row pins only that both passes agree.
/// </remarks>
public sealed class CommentBeforeTheSemicolonNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        const string source = """
                              class C {
                                void M() {
                                 IList<KeyValuePair<string, int>> v2 = JsonConvert.DeserializeObject<IList<KeyValuePair<string, int>>>(json) /* f */ ;
                                }
                              }
                              """;

        var first = FormatWith(source);
        Assert.DoesNotContain("(\n", first, StringComparison.Ordinal);
        Assert.Equal(first, FormatWith(first));
    }

    [Fact]
    public void TheCommentRidesOnTheValuesLine() {
        const string source = """
                              class C {
                                void M() {
                                 var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = JsonConvert.DeserializeObject<IList<KeyValuePair<string, int>>>(json) /* f */ ;
                                 var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = JsonConvert.DeserializeObject<IList<KeyValuePair<string, int>>>(json) /* f */ ;
                                }
                              }
                              """;
        const string oracle = """
                              class C {
                                  void M() {
                                      var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                          JsonConvert.DeserializeObject<IList<KeyValuePair<string, int>>>(json) /* f */;
                                      var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                          JsonConvert.DeserializeObject<IList<KeyValuePair<string, int>>>(json) /* f */;
                                  }
                              }

                              """;

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }
}
