using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A <c>/** */</c> comment between two tokens is laid out exactly as a <c>/* */</c> one of the same width,
///     and a comment after a parameter's attributes ends the line when the joined line overflows (Nightly
///     <c>fuzz --seed=55</c>, case 7447388608888272285; SK-DIV-0363).
/// </summary>
/// <remarks>
///     ⚠ Roslyn reads <c>/** d */</c> as documentation wherever it stands, and a dozen tests in
///     <c>BreakPlan</c> knew only <c>MultiLineCommentTrivia</c>. They all go through <c>IsBlockComment</c> now.
///     The parameter rows are the oracle's, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class DocumentationStyleCommentNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    // `/* dd */` and `/** d */` are both eight columns, so the two spellings must lay out alike.
    const string Shapes = """
                          class C {
                              [Obsolete] /* dd */ public static readonly Dictionary<string, int> Fieldxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = Create(alpha);
                              [Obsolete] /* dd */ public static readonly int Fieldyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy = alpha + beta + gamma;
                              public Dictionary<IReadOnlyList<string>, /* dd */ List<KeyValuePair<string, int>>> PropertyNameIsLongish { get; set; }
                              private Dictionary<IReadOnlyList<string>, /* dd */ List<KeyValuePair<string, int>>> fieldNameIsLongishxxxxxxxxx = null;
                              public void Method(Dictionary<IReadOnlyList<string>, /* dd */ List<KeyValuePair<string, int>>> parameterNameIsLongishxx) { }
                              public void Other(int a, [NotNull] /* dd */ Dictionary<string, List<int>> bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                              void M() {
                                  var valueNameIsLongish = SomeReceiver.SomeMethodName<GitHubPullRequestReview>(argumentNumberOne) /* dd */;
                                  var other = Compute(alphaArgumentIsLong, betaArgumentIsLong /* dd */, gammaArgumentIsLongToo, deltaArgumentIsLong);
                                  Call(alpha, /* dd */ beta, gamma);
                                  var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Callee12( /* dd */ aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                                  var x = /* dd */ SomeReceiver.SomeMethodName<GitHubPullRequestReview>(argumentNumberOne, argumentNumberTwo);
                              }
                          }
                          """;

    [Fact]
    public void ADocumentationStyleComment_LaysOutAsABlockComment() {
        var block = FormatWith(Shapes);
        var documentation = FormatWith(Shapes.Replace("/* dd */", "/** d */", StringComparison.Ordinal));
        Assert.Equal(block, documentation.Replace("/** d */", "/* dd */", StringComparison.Ordinal));
        Assert.Equal(documentation, FormatWith(documentation));
    }

    [Theory]
    [InlineData(
        "[NotNull] /* d */ Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy p10",
        "        [NotNull] /* d */\n        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy p10\n"
    )]
    [InlineData(
        "[InlineArray(8)] /** d */ Dictionary<AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA, BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB> p10",
        "        [InlineArray(8)] /** d */\n        Dictionary<AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA,\n            BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB> p10\n"
    )]
    public void ACommentAfterAParametersAttributes_EndsTheLine(string parameter, string expected) {
        var formatted = FormatWith(
            "class C {\n    public static int Create7(bool p8, TimeSpan p9, " + parameter + ") => 1;\n}\n"
        );
        Assert.Equal(
            "class C {\n    public static int Create7(\n        bool p8,\n        TimeSpan p9,\n"
            + expected
            + "    ) =>\n        1;\n}\n",
            formatted
        );
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
