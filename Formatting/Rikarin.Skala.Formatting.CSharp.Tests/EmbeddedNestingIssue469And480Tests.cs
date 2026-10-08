using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Embedded statements nested in embedded statements (issue #469) and the <c>else</c> or <c>while</c>
///     after one that is not a block (issue #480), both SK-DIV-0115.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration — <c>skala_keep_existing_embedded_arrangement = true</c> —
///     with <c>csharp_prefer_braces</c> off on Skala's side, because the oracle's <c>ask</c> inserts no
///     braces (SK-DIV-0100).
/// </remarks>
public sealed class EmbeddedNestingIssue469And480Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [
                    new KeyValuePair<string, string>("csharp_prefer_braces", "false"),
                    .. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))
                ]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Nestings = """
                            using System;
                            using System.Collections.Generic;
                            class C {
                                void M1(bool b, bool c, int n) {
                                    if (b) if (c) M();
                                }
                                void M2(bool b) {
                                    if (b) using (D()) M();
                                }
                                void M3(bool b, bool c) {
                                    if (b) while (c) M();
                                }
                                void M4(bool b, bool c) {
                                    while (b) if (c) M();
                                }
                                void M5(bool c, List<int> xs) {
                                    foreach (var x in xs) if (c) M();
                                }
                                void M6(bool b, bool c) {
                                    if (b)
                                        if (c) M();
                                }
                                void M7(bool b, bool c) {
                                    if (b) if (c)
                                        M();
                                }
                                void M8(bool c) {
                                    lock (this) if (c) M();
                                }
                                void M9(bool b) {
                                    if (b) for (;;) M();
                                }
                                void M10(bool a, bool b, bool c) {
                                    if (a) if (b) if (c) M();
                                }
                                void M11(bool b, bool c) {
                                    if (b) { if (c) M(); }
                                }
                                void M12(bool b, bool c) {
                                    if (b) M(); else while (c) M();
                                }
                                void M13() {
                                    using (D()) using (D()) M();
                                }
                                void M14(bool c, int n) {
                                    if (
                                    c) if (n > 0) n++;
                                }
                                void M15(bool b, bool c) {
                                    if (b) if (c) M(); else M();
                                }
                                void M16(bool b) {
                                    while (b) lock (this) M();
                                }
                                void M17(bool b) {
                                    do if (b) M(); while (b);
                                }
                                void M() {
                                }
                                IDisposable D() => null;
                            }
                            """;

    const string NestingsOracle = """
                                  using System;
                                  using System.Collections.Generic;

                                  class C {
                                      void M1(bool b, bool c, int n) {
                                          if (b)
                                              if (c)
                                                  M();
                                      }

                                      void M2(bool b) {
                                          if (b)
                                              using (D())
                                                  M();
                                      }

                                      void M3(bool b, bool c) {
                                          if (b)
                                              while (c)
                                                  M();
                                      }

                                      void M4(bool b, bool c) {
                                          while (b)
                                              if (c)
                                                  M();
                                      }

                                      void M5(bool c, List<int> xs) {
                                          foreach (var x in xs)
                                              if (c)
                                                  M();
                                      }

                                      void M6(bool b, bool c) {
                                          if (b)
                                              if (c)
                                                  M();
                                      }

                                      void M7(bool b, bool c) {
                                          if (b)
                                              if (c)
                                                  M();
                                      }

                                      void M8(bool c) {
                                          lock (this)
                                              if (c)
                                                  M();
                                      }

                                      void M9(bool b) {
                                          if (b)
                                              for (;;)
                                                  M();
                                      }

                                      void M10(bool a, bool b, bool c) {
                                          if (a)
                                              if (b)
                                                  if (c)
                                                      M();
                                      }

                                      void M11(bool b, bool c) {
                                          if (b) {
                                              if (c) M();
                                          }
                                      }

                                      void M12(bool b, bool c) {
                                          if (b) M();
                                          else
                                              while (c)
                                                  M();
                                      }

                                      void M13() {
                                          using (D())
                                          using (D())
                                              M();
                                      }

                                      void M14(bool c, int n) {
                                          if (
                                              c)
                                              if (n > 0)
                                                  n++;
                                      }

                                      void M15(bool b, bool c) {
                                          if (b)
                                              if (c) M();
                                              else M();
                                      }

                                      void M16(bool b) {
                                          while (b)
                                              lock (this)
                                                  M();
                                      }

                                      void M17(bool b) {
                                          do
                                              if (b)
                                                  M();
                                          while (b);
                                      }

                                      void M() { }
                                      IDisposable D() => null;
                                  }
                                  """;

    const string ElseNestings = """
                                using System.Collections.Generic;
                                class C {
                                    void M1(bool b, bool c) {
                                        if (b) M(); else if (c) M();
                                    }
                                    void M2(bool b, bool c) {
                                        if (b) if (c) M(); else M();
                                    }
                                    void M3(bool b, bool c) {
                                        while (b) if (c) M(); else M();
                                    }
                                    void M4(bool b, bool c, bool d) {
                                        if (b) if (c) M(); else if (d) M();
                                    }
                                    void M5(bool a, bool b, bool c) {
                                        if (a) M(); else if (b) if (c) M();
                                    }
                                    void M6(bool b, bool c) {
                                        if (b) M(); else if (c) while (c) M();
                                    }
                                    void M7(bool b, bool c) {
                                        if (b)
                                            if (c)
                                                M();
                                            else
                                                M();
                                    }
                                    void M8(bool b, bool c) {
                                        if (b) while (c) if (c) M(); else M();
                                    }
                                    void M9(bool b, bool c) {
                                        if (b) { if (c) M(); else M(); }
                                    }
                                    void M10(bool b, bool c) {
                                        if (b) M(); else { if (c) M(); }
                                    }
                                    void M11(List<int> xs, bool c) {
                                        foreach (var x in xs) foreach (var y in xs) M();
                                    }
                                    void M() {
                                    }
                                }
                                """;

    const string ElseNestingsOracle = """
                                      using System.Collections.Generic;

                                      class C {
                                          void M1(bool b, bool c) {
                                              if (b) M();
                                              else if (c) M();
                                          }

                                          void M2(bool b, bool c) {
                                              if (b)
                                                  if (c) M();
                                                  else M();
                                          }

                                          void M3(bool b, bool c) {
                                              while (b)
                                                  if (c) M();
                                                  else M();
                                          }

                                          void M4(bool b, bool c, bool d) {
                                              if (b)
                                                  if (c) M();
                                                  else if (d) M();
                                          }

                                          void M5(bool a, bool b, bool c) {
                                              if (a) M();
                                              else if (b)
                                                  if (c)
                                                      M();
                                          }

                                          void M6(bool b, bool c) {
                                              if (b) M();
                                              else if (c)
                                                  while (c)
                                                      M();
                                          }

                                          void M7(bool b, bool c) {
                                              if (b)
                                                  if (c)
                                                      M();
                                                  else
                                                      M();
                                          }

                                          void M8(bool b, bool c) {
                                              if (b)
                                                  while (c)
                                                      if (c) M();
                                                      else M();
                                          }

                                          void M9(bool b, bool c) {
                                              if (b) {
                                                  if (c) M();
                                                  else M();
                                              }
                                          }

                                          void M10(bool b, bool c) {
                                              if (b) M();
                                              else {
                                                  if (c) M();
                                              }
                                          }

                                          void M11(List<int> xs, bool c) {
                                              foreach (var x in xs)
                                              foreach (var y in xs)
                                                  M();
                                          }

                                          void M() { }
                                      }
                                      """;

    const string Clauses = """
                           class D {
                               void M1(bool b, int o) {
                                   if (b) M(); else switch (o) { case 1: break; }
                                   if (b) switch (o) { case 1: break; } else M();
                               }
                               void M2(bool b, bool c) {
                                   if (b) M(); else M();
                                   if (b) M(); else if (c) M(); else M();
                                   if (b)
                                       M();
                                   else M();
                                   if (b) M();
                                   else M();
                               }
                               void M3(bool b, bool c) {
                                   if (b) while (c) M(); else M();
                                   if (b) { M(); } else M();
                                   if (b) M(); else { M(); }
                               }
                               void M4(bool b, int o) {
                                   if (b) M(); else try { M(); } finally { M(); }
                                   if (b) try { M(); } finally { M(); } else M();
                               }
                               void M5(bool b, int o) {
                                   if (b) switch (o) { case 1: break; }
                                   M();
                               }
                               void M() {
                               }
                           }
                           """;

    const string ClausesOracle = """
                                 class D {
                                     void M1(bool b, int o) {
                                         if (b) M();
                                         else
                                             switch (o) {
                                                 case 1: break;
                                             }

                                         if (b)
                                             switch (o) {
                                                 case 1: break;
                                             }
                                         else M();
                                     }

                                     void M2(bool b, bool c) {
                                         if (b) M();
                                         else M();
                                         if (b) M();
                                         else if (c) M();
                                         else M();
                                         if (b)
                                             M();
                                         else M();
                                         if (b) M();
                                         else M();
                                     }

                                     void M3(bool b, bool c) {
                                         if (b)
                                             while (c)
                                                 M();
                                         else M();
                                         if (b) {
                                             M();
                                         } else M();

                                         if (b) M();
                                         else {
                                             M();
                                         }
                                     }

                                     void M4(bool b, int o) {
                                         if (b) M();
                                         else
                                             try {
                                                 M();
                                             } finally {
                                                 M();
                                             }

                                         if (b)
                                             try {
                                                 M();
                                             } finally {
                                                 M();
                                             }
                                         else M();
                                     }

                                     void M5(bool b, int o) {
                                         if (b)
                                             switch (o) {
                                                 case 1: break;
                                             }

                                         M();
                                     }

                                     void M() { }
                                 }
                                 """;

    const string ClausesUnderNewLineBeforeElseOracle = """
                                                       class D {
                                                           void M1(bool b, int o) {
                                                               if (b) M();
                                                               else
                                                                   switch (o) {
                                                                       case 1: break;
                                                                   }

                                                               if (b)
                                                                   switch (o) {
                                                                       case 1: break;
                                                                   }
                                                               else M();
                                                           }

                                                           void M2(bool b, bool c) {
                                                               if (b) M();
                                                               else M();
                                                               if (b) M();
                                                               else if (c) M();
                                                               else M();
                                                               if (b)
                                                                   M();
                                                               else M();
                                                               if (b) M();
                                                               else M();
                                                           }

                                                           void M3(bool b, bool c) {
                                                               if (b)
                                                                   while (c)
                                                                       M();
                                                               else M();
                                                               if (b) {
                                                                   M();
                                                               }
                                                               else M();

                                                               if (b) M();
                                                               else {
                                                                   M();
                                                               }
                                                           }

                                                           void M4(bool b, int o) {
                                                               if (b) M();
                                                               else
                                                                   try {
                                                                       M();
                                                                   } finally {
                                                                       M();
                                                                   }

                                                               if (b)
                                                                   try {
                                                                       M();
                                                                   } finally {
                                                                       M();
                                                                   }
                                                               else M();
                                                           }

                                                           void M5(bool b, int o) {
                                                               if (b)
                                                                   switch (o) {
                                                                       case 1: break;
                                                                   }

                                                               M();
                                                           }

                                                           void M() { }
                                                       }
                                                       """;

    const string BlankLines = """
                              class D {
                                  void M1(bool b, int o) {
                                      M();
                                      if (b) switch (o) { case 1: break; }
                                      M();
                                  }
                                  void M2(bool b, int o) {
                                      M();
                                      if (b) try { M(); } finally { M(); }
                                      M();
                                  }
                                  void M3(bool b, bool c) {
                                      M();
                                      if (b) if (c) { M(); }
                                      M();
                                  }
                                  void M4(bool b, bool c) {
                                      M();
                                      while (b) lock (this) { M(); }
                                      M();
                                  }
                                  void M5(bool b, bool c) {
                                      M();
                                      if (b) M(); else if (c) { M(); }
                                      M();
                                  }
                                  void M6(bool b, bool c) {
                                      M();
                                      if (b) M(); else while (c) { M(); }
                                      M();
                                  }
                                  void M7(bool b, bool c) {
                                      M();
                                      if (b) M(); else M();
                                      M();
                                  }
                                  void M() {
                                  }
                              }
                              """;

    const string BlankLinesOracle = """
                                    class D {
                                        void M1(bool b, int o) {
                                            M();
                                            if (b)
                                                switch (o) {
                                                    case 1: break;
                                                }

                                            M();
                                        }

                                        void M2(bool b, int o) {
                                            M();
                                            if (b)
                                                try {
                                                    M();
                                                } finally {
                                                    M();
                                                }

                                            M();
                                        }

                                        void M3(bool b, bool c) {
                                            M();
                                            if (b)
                                                if (c) {
                                                    M();
                                                }

                                            M();
                                        }

                                        void M4(bool b, bool c) {
                                            M();
                                            while (b)
                                                lock (this) {
                                                    M();
                                                }

                                            M();
                                        }

                                        void M5(bool b, bool c) {
                                            M();
                                            if (b) M();
                                            else if (c) {
                                                M();
                                            }

                                            M();
                                        }

                                        void M6(bool b, bool c) {
                                            M();
                                            if (b) M();
                                            else
                                                while (c) {
                                                    M();
                                                }

                                            M();
                                        }

                                        void M7(bool b, bool c) {
                                            M();
                                            if (b) M();
                                            else M();
                                            M();
                                        }

                                        void M() { }
                                    }
                                    """;

    const string BlankLinesBeforeBlockStatementsOracle = """
                                                         class D {
                                                             void M1(bool b, int o) {
                                                                 M();

                                                                 if (b)
                                                                     switch (o) {
                                                                         case 1: break;
                                                                     }

                                                                 M();
                                                             }

                                                             void M2(bool b, int o) {
                                                                 M();

                                                                 if (b)
                                                                     try {
                                                                         M();
                                                                     } finally {
                                                                         M();
                                                                     }

                                                                 M();
                                                             }

                                                             void M3(bool b, bool c) {
                                                                 M();

                                                                 if (b)
                                                                     if (c) {
                                                                         M();
                                                                     }

                                                                 M();
                                                             }

                                                             void M4(bool b, bool c) {
                                                                 M();

                                                                 while (b)
                                                                     lock (this) {
                                                                         M();
                                                                     }

                                                                 M();
                                                             }

                                                             void M5(bool b, bool c) {
                                                                 M();

                                                                 if (b) M();
                                                                 else if (c) {
                                                                     M();
                                                                 }

                                                                 M();
                                                             }

                                                             void M6(bool b, bool c) {
                                                                 M();

                                                                 if (b) M();
                                                                 else
                                                                     while (c) {
                                                                         M();
                                                                     }

                                                                 M();
                                                             }

                                                             void M7(bool b, bool c) {
                                                                 M();
                                                                 if (b) M();
                                                                 else M();
                                                                 M();
                                                             }

                                                             void M() { }
                                                         }
                                                         """;

    const string DoAndComment = """
                                class D {
                                    void M1(bool b) {
                                        do M(); while (b);
                                    }
                                    void M2(bool b) {
                                        do M();
                                        while (b);
                                    }
                                    void M3(bool b) {
                                        do
                                            M(); while (b);
                                    }
                                    void M4(bool b) {
                                        do { M(); } while (b);
                                    }
                                    void M5(bool b, int o) {
                                        do switch (o) { case 1: break; } while (b);
                                    }
                                    void M6(bool b) {
                                        if (b) M(); /* c */ else M();
                                    }
                                    void M7(bool b, int o) {
                                        if (b)
                                            M();
                                        else M();
                                    }
                                    void M() {
                                    }
                                }
                                """;

    const string DoAndCommentOracle = """
                                      class D {
                                          void M1(bool b) {
                                              do M();
                                              while (b);
                                          }

                                          void M2(bool b) {
                                              do M();
                                              while (b);
                                          }

                                          void M3(bool b) {
                                              do
                                                  M();
                                              while (b);
                                          }

                                          void M4(bool b) {
                                              do {
                                                  M();
                                              } while (b);
                                          }

                                          void M5(bool b, int o) {
                                              do
                                                  switch (o) {
                                                      case 1: break;
                                                  }
                                              while (b);
                                          }

                                          void M6(bool b) {
                                              if (b) M(); /* c */
                                              else M();
                                          }

                                          void M7(bool b, int o) {
                                              if (b)
                                                  M();
                                              else M();
                                          }

                                          void M() { }
                                      }
                                      """;

    const string KAndRClauses = """
                                class D {
                                    void M1(bool b) {
                                        if (b) {
                                            M();
                                        } else {
                                            M();
                                        }
                                        try {
                                            M();
                                        } catch {
                                            M();
                                        } finally {
                                            M();
                                        }
                                        do {
                                            M();
                                        } while (b);
                                        if (b) { M(); } else M();
                                    }
                                    void M() {
                                    }
                                }
                                """;

    const string KAndRClausesOracle = """
                                      class D {
                                          void M1(bool b) {
                                              if (b) {
                                                  M();
                                              }
                                              else {
                                                  M();
                                              }

                                              try {
                                                  M();
                                              }
                                              catch {
                                                  M();
                                              }
                                              finally {
                                                  M();
                                              }

                                              do {
                                                  M();
                                              }
                                              while (b);

                                              if (b) {
                                                  M();
                                              }
                                              else M();
                                          }

                                          void M() { }
                                      }
                                      """;

    public static TheoryData<string, string, string, string> Cases =>
        new() {
            { Nestings, NestingsOracle, "skala_keep_existing_embedded_arrangement", "true" },
            { ElseNestings, ElseNestingsOracle, "skala_keep_existing_embedded_arrangement", "true" },
            { Clauses, ClausesOracle, "skala_keep_existing_embedded_arrangement", "true" },
            { Clauses, ClausesUnderNewLineBeforeElseOracle, "skala_new_line_before_else", "true" },
            { BlankLines, BlankLinesOracle, "skala_keep_existing_embedded_arrangement", "true" },
            { BlankLines, BlankLinesBeforeBlockStatementsOracle, "skala_blank_lines_before_block_statements", "1" },
            { DoAndComment, DoAndCommentOracle, "skala_keep_existing_embedded_arrangement", "true" }
        };

    /// <summary>
    ///     ⚠ #469: an embedded statement that carries one of its own, or whose owner is itself embedded,
    ///     goes on a line of its own however short — except an <c>if</c> with an <c>else</c> and an
    ///     <c>else if</c>, which keep theirs. #480: an <c>else</c> or a <c>do</c>'s <c>while</c> after a
    ///     statement that is not a block starts a line, and a statement whose embedded statement has a
    ///     block takes the block-statement blank lines on both sides.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheInput_ComesBackAsTheOracleWritesIt(string source, string expected, string key, string value) {
        var formatted = FormatWith(source, (key, value));
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, (key, value)));
    }

    /// <summary>
    ///     ⚠ The placement keys' split direction: at <c>true</c> each of <c>skala_new_line_before_else</c>,
    ///     <c>_catch</c>, <c>_finally</c> and <c>_while</c> moves a keyword the author wrote after <c>}</c>
    ///     onto a line of its own. Skala only ever kept the author's break.
    /// </summary>
    [Fact]
    public void TheClauseKeys_SplitAKAndRInput() =>
        Assert.Equal(
            KAndRClausesOracle + "\n",
            FormatWith(
                KAndRClauses,
                ("skala_new_line_before_else", "true"),
                ("skala_new_line_before_catch", "true"),
                ("skala_new_line_before_finally", "true"),
                ("skala_new_line_before_while", "true")
            )
        );
}
