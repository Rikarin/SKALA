using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A query clause's keyword keeps its space at both <c>space_between_keyword_*</c> keys.
/// </summary>
/// <remarks>
///     ⚠ The oracle's answer, asked 2026-10-08 with <c>resharper_space_between_keyword_and_type = false</c>
///     and with <c>resharper_space_between_keyword_and_expression = false</c>: the input comes back unchanged
///     at both. Skala wrote <c>where"s"</c> under the first — <c>where</c> is on the keyword-and-type list for a
///     constraint clause — and <c>select(item)</c> under the second. Found by
///     <c>OptionObservabilityTests</c> on #576's fixture.
/// </remarks>
public sealed class QueryKeywordSpaceTests {
    const string Source = """
        class Q {
            object M(int[] items) {
                var a = from item in items where "s" == null select item;
                var b = from item in items where (item > 0) orderby item descending select (item);
                return a;
            }
        }
        """;

    [Theory]
    [InlineData("skala_space_between_keyword_and_type")]
    [InlineData("skala_space_between_keyword_and_expression")]
    public void AQueryKeyword_KeepsItsSpace_AtFalse(string key) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [new KeyValuePair<string, string>(key, "false")]
                )
                .Options
        );
        var formatted = CSharpFormatter.Format("Test.cs", SourceText.From(Source), options).Formatted;
        Assert.Equal(Source + "\n", formatted);
    }
}
