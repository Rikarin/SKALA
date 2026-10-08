using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>&amp;&amp;</c> and <c>||</c> are two chains (issue #565): a condition too wide for its line is
///     chopped at its <c>||</c>s, and each <c>&amp;&amp;</c> operand stays whole.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration.
/// </remarks>
public sealed class AndOrChainsIssue565Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Margin = """
                          class T {
                              bool M(bool alphaAlphaAlpha, bool betaBetaBetaBeta, bool gammaGammaGamma, bool deltaDeltaDelta, bool epsilonEpsilon) {
                                  if (alphaAlphaAlpha && betaBetaBetaBeta || gammaGammaGamma && deltaDeltaDelta || epsilonEpsilon && alphaAlphaAlpha) { }
                                  if (alphaAlphaAlpha || betaBetaBetaBeta && gammaGammaGamma || deltaDeltaDelta && epsilonEpsilon || alphaAlphaAlpha) { }
                                  if (alphaAlphaAlpha || betaBetaBetaBeta || gammaGammaGamma || deltaDeltaDelta && epsilonEpsilon && alphaAlphaAlpha) { }
                                  if (alphaAlphaAlpha && betaBetaBetaBeta && gammaGammaGamma && deltaDeltaDelta || epsilonEpsilon || alphaAlphaAlpha) { }
                                  var x = alphaAlphaAlpha || betaBetaBetaBeta && gammaGammaGamma || deltaDeltaDelta && epsilonEpsilon || alphaAlphaAlpha;
                                  return alphaAlphaAlpha && betaBetaBetaBeta || gammaGammaGamma && deltaDeltaDelta || epsilonEpsilon && alphaAlphaAlpha;
                              }
                          }
                          """;

    const string MarginOracle = """
                                class T {
                                    bool M(
                                        bool alphaAlphaAlpha,
                                        bool betaBetaBetaBeta,
                                        bool gammaGammaGamma,
                                        bool deltaDeltaDelta,
                                        bool epsilonEpsilon
                                    ) {
                                        if (alphaAlphaAlpha && betaBetaBetaBeta
                                            || gammaGammaGamma && deltaDeltaDelta
                                            || epsilonEpsilon && alphaAlphaAlpha) { }

                                        if (alphaAlphaAlpha
                                            || betaBetaBetaBeta && gammaGammaGamma
                                            || deltaDeltaDelta && epsilonEpsilon
                                            || alphaAlphaAlpha) { }

                                        if (alphaAlphaAlpha
                                            || betaBetaBetaBeta
                                            || gammaGammaGamma
                                            || deltaDeltaDelta && epsilonEpsilon && alphaAlphaAlpha) { }

                                        if (alphaAlphaAlpha && betaBetaBetaBeta && gammaGammaGamma && deltaDeltaDelta
                                            || epsilonEpsilon
                                            || alphaAlphaAlpha) { }

                                        var x = alphaAlphaAlpha
                                            || betaBetaBetaBeta && gammaGammaGamma
                                            || deltaDeltaDelta && epsilonEpsilon
                                            || alphaAlphaAlpha;
                                        return alphaAlphaAlpha && betaBetaBetaBeta
                                            || gammaGammaGamma && deltaDeltaDelta
                                            || epsilonEpsilon && alphaAlphaAlpha;
                                    }
                                }
                                """;

    const string TheIssue = """
                            using System.Collections.Generic;
                            class T {
                                HashSet<int>? captured;
                                void M1(object node) {
                                    foreach (var paren in new List<object>()) {
                                        if (paren is not string { Length: > 0 } collection
                                            || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                            return;
                                        }
                                    }
                                }
                                void M2(object node) {
                                    foreach (var paren in new List<object>()) {
                                        if (paren is not string { Length: > 0 } collection
                                            || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                            continue;
                                        }
                                    }
                                }
                                void M3(object paren) {
                                    if (paren is not string { Length: > 0 } collection
                                        || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                        return;
                                    }
                                }
                                bool IsInsideCaptured(object o) => true;
                            }
                            """;

    const string TheIssueOracle = """
                                  using System.Collections.Generic;

                                  class T {
                                      HashSet<int>? captured;

                                      void M1(object node) {
                                          foreach (var paren in new List<object>()) {
                                              if (paren is not string { Length: > 0 } collection
                                                  || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                                  return;
                                              }
                                          }
                                      }

                                      void M2(object node) {
                                          foreach (var paren in new List<object>()) {
                                              if (paren is not string { Length: > 0 } collection
                                                  || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                                  continue;
                                              }
                                          }
                                      }

                                      void M3(object paren) {
                                          if (paren is not string { Length: > 0 } collection
                                              || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                              return;
                                          }
                                      }

                                      bool IsInsideCaptured(object o) => true;
                                  }
                                  """;

    const string AuthorsBreaks = """
                                 using System.Collections.Generic;
                                 class T {
                                     void M(bool a, bool b, List<int> c, bool d, bool e) {
                                         if (a
                                             || b && d) { }
                                         if (a && b
                                             || d) { }
                                         if (a
                                             || b && d
                                             || e) { }
                                         if (a
                                             && b || d) { }
                                         if (a
                                             || b
                                             && d) { }
                                         if (a || b
                                             && d || e) { }
                                         var x = a
                                             && b || d && e;
                                         if (a && b ||
                                             d) { }
                                     }
                                 }
                                 """;

    const string AuthorsBreaksOracle = """
                                       using System.Collections.Generic;

                                       class T {
                                           void M(bool a, bool b, List<int> c, bool d, bool e) {
                                               if (a
                                                   || b && d) { }

                                               if (a && b
                                                   || d) { }

                                               if (a
                                                   || b && d
                                                   || e) { }

                                               if (a
                                                   && b
                                                   || d) { }

                                               if (a
                                                   || b
                                                   && d) { }

                                               if (a
                                                   || b
                                                   && d
                                                   || e) { }

                                               var x = a
                                                   && b
                                                   || d && e;
                                               if (a && b || d) { }
                                           }
                                       }
                                       """;

    const string Neighbours = """
                              using System.Collections.Generic;
                              class T {
                                  HashSet<int>? captured;
                                  void M(object node) {
                                      foreach (var paren in new List<object>()) {
                                          if (paren is not string { Length: > 0 } collection
                                              || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                              continue;
                                          }
                                      }
                                      foreach (var open in new List<object>()) {
                                          if (!open.Equals(1)
                                              || !open.Equals(2)
                                              || (open.GetHashCode() & 4) == 0
                                              || captured is { Count: > 0 } && open.ToString() is { } parent && IsInsideCaptured(parent)) {
                                              continue;
                                          }
                                      }
                                  }
                                  bool IsInsideCaptured(object o) => true;
                              }
                              """;

    const string NeighboursOracle = """
                                    using System.Collections.Generic;

                                    class T {
                                        HashSet<int>? captured;

                                        void M(object node) {
                                            foreach (var paren in new List<object>()) {
                                                if (paren is not string { Length: > 0 } collection
                                                    || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                                                    continue;
                                                }
                                            }

                                            foreach (var open in new List<object>()) {
                                                if (!open.Equals(1)
                                                    || !open.Equals(2)
                                                    || (open.GetHashCode() & 4) == 0
                                                    || captured is { Count: > 0 } && open.ToString() is { } parent && IsInsideCaptured(parent)) {
                                                    continue;
                                                }
                                            }
                                        }

                                        bool IsInsideCaptured(object o) => true;
                                    }
                                    """;

    public static TheoryData<string, string> Cases =>
        new() {
            { Margin, MarginOracle },
            { TheIssue, TheIssueOracle },
            { AuthorsBreaks, AuthorsBreaksOracle },
            { Neighbours, NeighboursOracle }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCondition_ComesBackAsTheOracleWritesIt(string source, string expected) {
        var formatted = FormatWith(source);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
