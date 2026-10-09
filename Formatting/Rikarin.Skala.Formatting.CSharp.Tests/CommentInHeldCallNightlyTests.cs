using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A block comment inside a held call's type arguments counts toward the value's width (Nightly
///     <c>fuzz --seed=909</c>, case 6285859913225113725).
/// </summary>
/// <remarks>
///     ⚠ <c>BreakPlan.FlatSourceWidth</c> skipped the comment, so #528's held-call table kept the <c>=</c> and
///     chopped the arguments of a line the comment had pushed past the margin; pass two read the chop as the
///     author's and broke the <c>=</c>. The expected answer is the oracle's for both inputs, measured
///     2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CommentInHeldCallNightlyTests {
    const string Long1 = "var jsonObjectWithLowercase = JsonConvert.DeserializeObject<GitHubPullRequestRev"
        + "iew /* f */ >(jsonWithLowercase);";

    const string Long2 = "var jsonObjectWithLowercase = JsonConvert.DeserializeObject<GitHubPullRequestRev"
        + "iew>(jsonWithLowercase); // fuzz";

    const string Long3 = "var jsonObjectWithUppercase = JsonConvert /** d */ .DeserializeObject<GitHubPull"
        + "RequestReview>(jsonWithUppercase);";

    const string Long4 = "var jsonObjectWithUppercase = JsonConvert /* d */ .DeserializeObject<GitHubPullR"
        + "equestReview>(jsonWithUppercase);";

    const string Long5 = "var jsonObjectWithUppercase = JsonConvert /** dd */ .DeserializeObject<GitHubPul"
        + "lRequestReview>(jsonWithUpper);";

    const string Long6 = "var jsonObjectWithUppercase = JsonConvert.DeserializeObject<GitHubPullRequestRev"
        + "iew /** d */>(jsonWithUppercase);";

    const string Long7 = "var jsonObjectWithUppercase = JsonConvert.DeserializeObject<GitHubPullRequestRev"
        + "iew>(jsonWithUppercase /** dddd */);";

    const string Long8 = "JsonConvert /** d */.DeserializeObject<GitHubPullRequestReview>(jsonWithUppercas"
        + "e);";

    const string Long9 = "JsonConvert /* d */.DeserializeObject<GitHubPullRequestReview>(jsonWithUppercase"
        + ");";

    const string Long10 = "JsonConvert /** dd */.DeserializeObject<GitHubPullRequestReview>(jsonWithUpper);";

    const string Long11 = "JsonConvert.DeserializeObject<GitHubPullRequestReview /** d */>(jsonWithUppercas"
        + "e);";

    const string Long12 = "JsonConvert.DeserializeObject<GitHubPullRequestReview>(jsonWithUppercase /** ddd"
        + "d */);";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    // ⚠ Inside a namespace, as the case was found: at a statement indent of 8 the line is exactly 120 columns
    // and stays whole.
    const string Minimised = $$"""
                               namespace N {
                               class C {
                                 public void M()
                                 {
                                  {{Long1}}
                                 }
                               }
                               }
                               """;

    [Fact]
    public void TheMinimisedCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Contains(
            "            var jsonObjectWithLowercase =\n"
            + "                JsonConvert.DeserializeObject<GitHubPullRequestReview /* f */>(jsonWithLowercase);\n",
            first,
            StringComparison.Ordinal
        );
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     A line comment after the <c>;</c> pushes the line past the margin, and travels with the value
    ///     (<c>fuzz --seed=3</c>, case 7754551050098241345); the oracle breaks the <c>=</c> for both inputs.
    /// </summary>
    [Fact]
    public void ATrailingComment_CountsTowardTheHeldValue() {
        const string source = $$"""
                                namespace N {
                                class C {
                                  public void M()
                                  {
                                   {{Long2}}
                                  }
                                }
                                }
                                """;

        var first = FormatWith(source);
        Assert.Contains(
            "            var jsonObjectWithLowercase =\n"
            + "                JsonConvert.DeserializeObject<GitHubPullRequestReview>(jsonWithLowercase); // fuzz\n",
            first,
            StringComparison.Ordinal
        );
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     ⚠ A <c>/** d */</c> comment, which Roslyn reads as documentation, counts as the block comment it is
    ///     written as — through its <c>/**</c>, which lies outside the trivia's <c>Span</c> (Nightly fuzz, case
    ///     1267273925188459665). The third row is the line one column too long only with the comment's full
    ///     width: <c>/** dd */</c> measured as <c>dd */</c> kept its <c>=</c> and chopped the call.
    /// </summary>
    [Fact]
    public void ADocumentationStyleComment_CountsInFull() {
        const string source = $$"""
                                namespace N {
                                class C {
                                  public void M()
                                  {
                                   {{Long3}}
                                   {{Long4}}
                                   {{Long5}}
                                   {{Long6}}
                                   {{Long7}}
                                  }
                                }
                                }
                                """;
        const string oracle = $$"""
                                namespace N {
                                    class C {
                                        public void M() {
                                            var jsonObjectWithUppercase =
                                                {{Long8}}
                                            var jsonObjectWithUppercase =
                                                {{Long9}}
                                            var jsonObjectWithUppercase =
                                                {{Long10}}
                                            var jsonObjectWithUppercase =
                                                {{Long11}}
                                            var jsonObjectWithUppercase =
                                                {{Long12}}
                                        }
                                    }
                                }

                                """;

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }
}
