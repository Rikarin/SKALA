using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A conditional broken at one sign is chopped at both (issue #518).
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration and at <c>skala_keep_user_linebreaks = false</c>.
/// </remarks>
public sealed class ConditionalChopIssue518Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [
                    .. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))
                ]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class T {
                              int M(bool b, int a, int c) {
                                  int x = b ? a
                                  : c;
                                  var y = a
                                  /* c */
                                  ? 1 : 2;
                                  int z = b
                                  ? a : c;
                                  F(b ? a
                                  : c);
                                  int v = b ? F(a,
                                  c) : c;
                                  return b ? a
                                  : c;
                              }
                              int F(int a, int b = 0) => a;
                          }
                          """;

    const string Oracle = """
                          class T {
                              int M(bool b, int a, int c) {
                                  int x = b
                                      ? a
                                      : c;
                                  var y = a
                                      /* c */
                                      ? 1
                                      : 2;
                                  int z = b
                                      ? a
                                      : c;
                                  F(
                                      b
                                          ? a
                                          : c
                                  );
                                  int v = b
                                      ? F(
                                          a,
                                          c
                                      )
                                      : c;
                                  return b
                                      ? a
                                      : c;
                              }

                              int F(int a, int b = 0) => a;
                          }
                          """;

    const string AtKeepFalse = """
                               class T {
                                   int M(bool b, int a, int c) {
                                       int x = b ? a : c;
                                       var y = a
                                           /* c */
                                           ? 1
                                           : 2;
                                       int z = b ? a : c;
                                       F(b ? a : c);
                                       int v = b ? F(a, c) : c;
                                       return b ? a : c;
                                   }

                                   int F(int a, int b = 0) => a;
                               }
                               """;

    public static TheoryData<string, string, string> Cases =>
        new() { { Source, Oracle, string.Empty }, { Source, AtKeepFalse, "skala_keep_user_linebreaks=false" } };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AConditionalBrokenAtOneSign_ComesBackAsTheOracleWritesIt(
        string source,
        string expected,
        string settings
    ) {
        var overrides = settings.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => (s.Split('=')[0], s.Split('=')[1]))
            .ToArray();
        var formatted = FormatWith(source, overrides);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, overrides));
    }
}
