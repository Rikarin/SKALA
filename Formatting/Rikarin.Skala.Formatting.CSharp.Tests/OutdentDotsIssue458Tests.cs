using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #458: <c>skala_outdent_dots = true</c> outdents each wrapped line by its own leading
///     operator's width — a <c>?.</c> line by two columns, a <c>.</c> line by one. Every expected string is
///     <c>jb cleanupcode</c>'s own output with the key flipped, measured 2026-10-08 with <c>Testing ask</c>.
/// </summary>
/// <remarks>
///     ⚠ Not in <c>constructs/alignment/outdent.cs</c>, which is at the export's <c>false</c>; a row there
///     cannot pin a value the export does not set (SK-DIV-0069).
/// </remarks>
public sealed class OutdentDotsIssue458Tests {
    static string Format(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [new KeyValuePair<string, string>("skala_outdent_dots", "true")]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string Statement(string statement) =>
        $$"""
          class T {
              void M() {
                  {{statement}}
              }
          }
          """;

    /// <summary>
    ///     The issue's chain, a chain with two <c>?.</c> lines and a trailing property, and one whose
    ///     trailing property follows a <c>?</c>. Before the fix every <c>?.</c> line sat one column right
    ///     of the oracle's.
    /// </summary>
    [Theory]
    [InlineData(
        "var result = someCollectionOfThingsHere?.WhereEnabled()?.SelectName(item => item.Name).OrderByName(name => name).ToList();",
        """
        var result = someCollectionOfThingsHere?.WhereEnabled()
                  ?.SelectName(item => item.Name)
                   .OrderByName(name => name)
                   .ToList();
        """
    )]
    [InlineData(
        "var result3 = someCollectionOfThingsHere.WhereEnabled()?.SelectName(item => item.Name)?.OrderByName(name => name).Count;",
        """
        var result3 = someCollectionOfThingsHere.WhereEnabled()
                  ?.SelectName(item => item.Name)
                  ?.OrderByName(name => name)
                   .Count;
        """
    )]
    [InlineData(
        "var result5 = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).OrderBy(n => n).ToList()?.Count;",
        """
        var result5 = someCollectionOfThingsHere.Where(c => c.IsEnabled)
                   .Select(c => c.Name)
                   .OrderBy(n => n)
                   .ToList()
                  ?.Count;
        """
    )]
    public void EachLine_IsOutdentedByItsOwnOperator(string input, string expected) {
        var once = Format(Statement(input));
        Assert.Equal(Statement(expected), once.TrimEnd('\n'));
        Assert.Equal(once, Format(once));
    }
}
