using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A collection expression that is a grouping parenthesis's whole contents (issue #485,
///     SK-DIV-0150): its <c>[</c> joins the <c>(</c> once it breaks, and the two-grouping rows beside it.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration and at <c>skala_keep_user_linebreaks = false</c>.
/// </remarks>
public sealed class ParenthesisedCollectionIssue485Tests {
    static string FormatWith(string source, string settings) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [
                        ..settings.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(static s => new KeyValuePair<string, string>(s.Split('=')[0], s.Split('=')[1]))
                    ]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Rows = """
                        class T {
                            object M(bool c, int a, int b) {
                                var x1 = ((c
                                ? a
                                : b));
                                var x2 = ((a
                                + b));
                                var x3 = (((a
                                + b)));
                                if (((c
                                || c))) { }
                                int[] z1 = ([
                                1,
                                2
                                ]);
                                int[] z2 = (
                                [
                                1,
                                2
                                ]);
                                int[] z3 = ( [
                                1,
                                2
                                ]);
                                N(([
                                1,
                                2
                                ]));
                                int[] z4 = (([
                                1,
                                2
                                ]));
                                int[] z5 = ([1, 2]);
                                int[] z6 = (
                                [1, 2]);
                                return x1;
                            }
                            void N(int[] a) {
                            }
                        }
                        """;

    const string RowsOracle = """
                              class T {
                                  object M(bool c, int a, int b) {
                                      var x1 = ((c
                                          ? a
                                          : b));
                                      var x2 = ((a
                                          + b));
                                      var x3 = (((a
                                          + b)));
                                      if (((c
                                              || c))) { }

                                      int[] z1 = ( [
                                          1,
                                          2
                                      ]);
                                      int[] z2 = ( [
                                          1,
                                          2
                                      ]);
                                      int[] z3 = ( [
                                          1,
                                          2
                                      ]);
                                      N(
                                          ( [
                                              1,
                                              2
                                          ])
                                      );
                                      int[] z4 = (( [
                                          1,
                                          2
                                      ]));
                                      int[] z5 = ([1, 2]);
                                      int[] z6 = (
                                          [1, 2]);
                                      return x1;
                                  }

                                  void N(int[] a) { }
                              }
                              """;

    const string RowsAtKeepFalse = """
                                   class T {
                                       object M(bool c, int a, int b) {
                                           var x1 = ((c ? a : b));
                                           var x2 = ((a + b));
                                           var x3 = (((a + b)));
                                           if (((c || c))) { }

                                           int[] z1 = ( [
                                               1,
                                               2
                                           ]);
                                           int[] z2 = ( [
                                               1,
                                               2
                                           ]);
                                           int[] z3 = ( [
                                               1,
                                               2
                                           ]);
                                           N(
                                               ( [
                                                   1,
                                                   2
                                               ])
                                           );
                                           int[] z4 = (( [
                                               1,
                                               2
                                           ]));
                                           int[] z5 = ([1, 2]);
                                           int[] z6 = ([1, 2]);
                                           return x1;
                                       }

                                       void N(int[] a) { }
                                   }
                                   """;

    const string Breaks = """
                          class T {
                              int[] M() {
                                  int[] z1 = ((
                                  [
                                  1,
                                  2
                                  ]));
                                  int[] z2 = (
                                  [1,
                                  2]);
                                  int[] z3 = (
                                  [
                                  1
                                  ]);
                                  N(
                                  (
                                  [
                                  1,
                                  2
                                  ]));
                                  return (
                                  [
                                  1,
                                  2
                                  ]);
                              }
                              void N(int[] a) {
                              }
                          }
                          """;

    const string BreaksOracle = """
                                class T {
                                    int[] M() {
                                        int[] z1 = (( [
                                            1,
                                            2
                                        ]));
                                        int[] z2 = ( [
                                            1,
                                            2
                                        ]);
                                        int[] z3 = ( [
                                            1
                                        ]);
                                        N(
                                            ( [
                                                1,
                                                2
                                            ])
                                        );
                                        return ( [
                                            1,
                                            2
                                        ]);
                                    }

                                    void N(int[] a) { }
                                }
                                """;

    const string BreaksAtKeepFalse = """
                                     class T {
                                         int[] M() {
                                             int[] z1 = (( [
                                                 1,
                                                 2
                                             ]));
                                             int[] z2 = ( [
                                                 1,
                                                 2
                                             ]);
                                             int[] z3 = ( [
                                                 1
                                             ]);
                                             N(
                                                 ( [
                                                     1,
                                                     2
                                                 ])
                                             );
                                             return ( [
                                                 1,
                                                 2
                                             ]);
                                         }

                                         void N(int[] a) { }
                                     }
                                     """;

    public static TheoryData<string, string, string> Cases =>
        new() {
            { Rows, RowsOracle, string.Empty },
            { Rows, RowsAtKeepFalse, "skala_keep_user_linebreaks=false" },
            { Breaks, BreaksOracle, string.Empty },
            { Breaks, BreaksAtKeepFalse, "skala_keep_user_linebreaks=false" }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCollection_ComesBackAsTheOracleWritesIt(string source, string expected, string settings) {
        var formatted = FormatWith(source, settings);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, settings));
    }
}
