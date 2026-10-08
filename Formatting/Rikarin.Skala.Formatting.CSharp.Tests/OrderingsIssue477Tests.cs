using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>orderby</c> clause's orderings (issue #477, SK-DIV-0114): a fill one continuation level past
///     the clause.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration and with <c>skala_keep_user_linebreaks = false</c> and
///     <c>skala_wrap_before_comma = true</c> flipped one at a time.
/// </remarks>
public sealed class OrderingsIssue477Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Statements = """
                              using System.Linq;
                              using System.Collections.Generic;
                              class Q {
                                  void M1(List<int> xs) {
                                      var q = from a in xs
                                      orderby a,
                                      b
                                      select a;
                                  }
                                  void M2(List<int> xs) {
                                      var q = from a in xs
                                      orderby a
                                      , a
                                      select a;
                                  }
                                  void M3(List<int> xs) {
                                      var q = from a in xs
                                      orderby a descending,
                                      a ascending
                                      select a;
                                  }
                                  void M4(List<int> xs) {
                                      var q = from a in xs
                                      orderby a,
                                      a,
                                      a
                                      select a;
                                  }
                                  void M5(List<int> xs) {
                                      var q = from a in xs orderby a, a select a;
                                  }
                                  void M6(List<int> xs) {
                                      var q = from aaaaaaaaaaaaaaaaaaaaaaaaaa in xs
                                          orderby aaaaaaaaaaaaaaaaaaaaaaaaaa.ToString().Length, aaaaaaaaaaaaaaaaaaaaaaaaaa.ToString().Length, aaaaaaaaaaaaaaaaaaaaaaaaaa
                                          select aaaaaaaaaaaaaaaaaaaaaaaaaa;
                                  }
                              }
                              """;

    const string StatementsOracle = """
                                    using System.Linq;
                                    using System.Collections.Generic;

                                    class Q {
                                        void M1(List<int> xs) {
                                            var q = from a in xs
                                                orderby a,
                                                    b
                                                select a;
                                        }

                                        void M2(List<int> xs) {
                                            var q = from a in xs
                                                orderby a
                                                    , a
                                                select a;
                                        }

                                        void M3(List<int> xs) {
                                            var q = from a in xs
                                                orderby a descending,
                                                    a ascending
                                                select a;
                                        }

                                        void M4(List<int> xs) {
                                            var q = from a in xs
                                                orderby a,
                                                    a,
                                                    a
                                                select a;
                                        }

                                        void M5(List<int> xs) {
                                            var q = from a in xs orderby a, a select a;
                                        }

                                        void M6(List<int> xs) {
                                            var q = from aaaaaaaaaaaaaaaaaaaaaaaaaa in xs
                                                orderby aaaaaaaaaaaaaaaaaaaaaaaaaa.ToString().Length, aaaaaaaaaaaaaaaaaaaaaaaaaa.ToString().Length,
                                                    aaaaaaaaaaaaaaaaaaaaaaaaaa
                                                select aaaaaaaaaaaaaaaaaaaaaaaaaa;
                                        }
                                    }
                                    """;

    const string Owners = """
                          using System.Linq;
                          using System.Collections.Generic;
                          class Q {
                              IEnumerable<int> M1(List<int> xs) =>
                                  from a in xs
                                  orderby a,
                                  a
                                  select a;
                              void M2(List<int> xs) {
                                  Use(from a in xs
                                      orderby a,
                                      a
                                      select a);
                              }
                              IEnumerable<int> M3(List<int> xs) {
                                  return from a in xs orderby a.ToString().Length, a.ToString().Length, a.ToString().Length, a.ToString().Length, a select a;
                              }
                              void M4(List<int> xs) {
                                  var q = from a in xs
                                  orderby a
                                  , a
                                  , a
                                  select a;
                              }
                              void Use(IEnumerable<int> q) {
                              }
                          }
                          """;

    const string OwnersOracle = """
                                using System.Linq;
                                using System.Collections.Generic;

                                class Q {
                                    IEnumerable<int> M1(List<int> xs) =>
                                        from a in xs
                                        orderby a,
                                            a
                                        select a;

                                    void M2(List<int> xs) {
                                        Use(
                                            from a in xs
                                            orderby a,
                                                a
                                            select a
                                        );
                                    }

                                    IEnumerable<int> M3(List<int> xs) {
                                        return from a in xs
                                            orderby a.ToString().Length, a.ToString().Length, a.ToString().Length, a.ToString().Length, a
                                            select a;
                                    }

                                    void M4(List<int> xs) {
                                        var q = from a in xs
                                            orderby a
                                                , a
                                                , a
                                            select a;
                                    }

                                    void Use(IEnumerable<int> q) { }
                                }
                                """;

    const string OwnersAtKeepFalse = """
                                     using System.Linq;
                                     using System.Collections.Generic;

                                     class Q {
                                         IEnumerable<int> M1(List<int> xs) => from a in xs orderby a, a select a;

                                         void M2(List<int> xs) {
                                             Use(from a in xs orderby a, a select a);
                                         }

                                         IEnumerable<int> M3(List<int> xs) {
                                             return from a in xs
                                                 orderby a.ToString().Length, a.ToString().Length, a.ToString().Length, a.ToString().Length, a
                                                 select a;
                                         }

                                         void M4(List<int> xs) {
                                             var q = from a in xs orderby a, a, a select a;
                                         }

                                         void Use(IEnumerable<int> q) { }
                                     }
                                     """;

    const string OwnersBeforeComma = """
                                     using System.Linq;
                                     using System.Collections.Generic;

                                     class Q {
                                         IEnumerable<int> M1(List<int> xs) =>
                                             from a in xs
                                             orderby a,
                                                 a
                                             select a;

                                         void M2(List<int> xs) {
                                             Use(
                                                 from a in xs
                                                 orderby a,
                                                     a
                                                 select a
                                             );
                                         }

                                         IEnumerable<int> M3(List<int> xs) {
                                             return from a in xs
                                                 orderby a.ToString().Length, a.ToString().Length, a.ToString().Length, a.ToString().Length, a
                                                 select a;
                                         }

                                         void M4(List<int> xs) {
                                             var q = from a in xs
                                                 orderby a
                                                     , a
                                                     , a
                                                 select a;
                                         }

                                         void Use(IEnumerable<int> q) { }
                                     }
                                     """;

    public static TheoryData<string, string, string, string> Cases =>
        new() {
            { Statements, StatementsOracle, "skala_keep_user_linebreaks", "true" },
            { Owners, OwnersOracle, "skala_keep_user_linebreaks", "true" },
            { Owners, OwnersAtKeepFalse, "skala_keep_user_linebreaks", "false" },
            { Owners, OwnersBeforeComma, "skala_wrap_before_comma", "true" }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheOrderings_ComeBackAsTheOracleWritesThem(string source, string expected, string key, string value) {
        var formatted = FormatWith(source, (key, value));
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, (key, value)));
    }
}
