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
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = """
                             class C {
                               public void M()
                               {
                                var jsonObjectWithLowercase = JsonConvert.DeserializeObject<GitHubPullRequestReview /* f */ >(jsonWithLowercase);
                               }
                             }
                             """;

    [Fact]
    public void TheMinimisedCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Contains(
            "        var jsonObjectWithLowercase =\n"
            + "            JsonConvert.DeserializeObject<GitHubPullRequestReview /* f */>(jsonWithLowercase);\n",
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
        const string source = """
                              namespace N {
                              class C {
                                public void M()
                                {
                                 var jsonObjectWithLowercase = JsonConvert.DeserializeObject<GitHubPullRequestReview>(jsonWithLowercase); // fuzz
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
}
