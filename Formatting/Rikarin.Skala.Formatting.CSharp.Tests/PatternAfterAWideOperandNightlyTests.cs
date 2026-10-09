using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A local's <c>=</c> before <c>operand is A or B</c> breaks when the line through the first alternative
///     would end past the margin, and the <c>or</c>s after an <c>is</c> that broke share <c>A</c>'s column
///     (Nightly <c>fuzz --seed=7777</c>, case 16865623964709448456).
/// </summary>
/// <remarks>
///     ⚠ Three faults made one finding. #446's table was measured with short operands and, behind a long
///     one, kept <c>T v = operand is</c> and broke after the <c>is</c>; the chain after that break spent its
///     own level, putting the <c>or</c>s a level past <c>A</c>; and pass two, reading the <c>is</c> break as
///     the author's, put them back on <c>A</c>'s column. The fuzzer reached it through a line comment in the
///     pattern, which the planner turned away from the table altogether. Every expected output is the
///     oracle's, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class PatternAfterAWideOperandNightlyTests {
    const string Long1 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBB"
        + "BBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long2 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBB"
        + "BBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB o"
        + "r BBBBBBBBBBBBBBBBBBBB;";

    const string Long3 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or "
        + "BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBB"
        + "BBBBBBBBBBBB;";

    const string Long4 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or"
        + " BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBB"
        + "BBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long5 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBB"
        + "BBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB o"
        + "r BBBBBBBBBBBBBBBBBBBB;";

    const string Long6 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBB"
        + "BBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB "
        + "or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long7 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBB"
        + "BBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBB"
        + "BBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long8 = "bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBB"
        + "BBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBB"
        + "BBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long9 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBB"
        + "BBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long10 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBB"
        + "BBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or"
        + " BBBBBBBBBBBBBBBBBBBB;";

    const string Long11 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or B"
        + "BBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBB"
        + "BBBBBBBBBBB;";

    const string Long12 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or "
        + "BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBB"
        + "BBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long13 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBB"
        + "BBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or"
        + " BBBBBBBBBBBBBBBBBBBB;";

    const string Long14 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "oooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBB"
        + "BBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB o"
        + "r BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long15 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBB"
        + "BBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBB"
        + "BB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long16 = "var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo"
        + "ooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBB"
        + "BBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBB"
        + "BBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;";

    const string Long17 = "or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBB"
        + "BBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB;";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static readonly string Minimised = $$"""
                                         class EqualsBeforeABinaryPattern {
                                           void M() { // fuzz
                                           bool c = {{R('o', 82)}} is Aaaaaaaaaaaaa // fuzz
                                         or Bbbbbbbbbbbbb; // fuzz
                                           }
                                         }
                                         """;

    static readonly string MinimisedOracle = $$"""
                                               class EqualsBeforeABinaryPattern {
                                                   void M() { // fuzz
                                                       bool c =
                                                           {{R('o', 82)}} is Aaaaaaaaaaaaa // fuzz
                                                               or Bbbbbbbbbbbbb; // fuzz
                                                   }
                                               }
                                               """;

    static readonly string Grid = $$"""
                                    class C {
                                        void M() {
                                            {{Long1}}
                                            bool c = {{R('o', 75)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long2}}
                                            bool c = {{R('o', 90)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long3}}
                                            bool c = {{R('o', 90)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long4}}
                                            bool c = {{R('o', 100)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long5}}
                                            bool c = {{R('o', 100)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long6}}
                                            bool c = {{R('o', 105)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long7}}
                                            bool c = {{R('o', 105)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long8}}
                                            var c = {{R('o', 75)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long9}}
                                            var c = {{R('o', 75)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long10}}
                                            {{Long11}}
                                            var c = {{R('o', 90)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long12}}
                                            var c = {{R('o', 100)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long13}}
                                            var c = {{R('o', 100)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long14}}
                                            var c = {{R('o', 105)}} is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long15}}
                                            var c = {{R('o', 105)}} is {{R('A', 30)}} or BBBBBBBBBBBBBBBBBBBB;
                                            {{Long16}}
                                        }
                                    }
                                    """;

    static readonly string GridOracle = $$"""
                                          class C {
                                              void M() {
                                                  bool c = {{R('o', 75)}} is AAAAA
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 75)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 75)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 90)}} is AAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c = {{R('o', 90)}} is AAAAA
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 90)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 90)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 100)}} is
                                                          AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 100)}} is
                                                          AAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 100)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 100)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 105)}} is
                                                          AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 105)}} is
                                                          AAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 105)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  bool c =
                                                      {{R('o', 105)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c = {{R('o', 75)}} is AAAAA
                                                      or BBBBBBBBBBBBBBBBBBBB;
                                                  var c = {{R('o', 75)}} is AAAAA
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 75)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 75)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c = {{R('o', 90)}} is AAAAA
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB
                                                      or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 90)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 90)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 100)}} is
                                                          AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 100)}} is
                                                          AAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 100)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 100)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 105)}} is
                                                          AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 105)}} is
                                                          AAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 105)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                                  var c =
                                                      {{R('o', 105)}} is
                                                          AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBBBBBBBBB;
                                              }
                                          }
                                          """;

    static readonly string Comments = $$"""
                                        class C {
                                            void M() {
                                                bool c = {{R('o', 78)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                bool c = {{R('o', 80)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                bool c = {{R('o', 84)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                var c = {{R('o', 79)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                var c = {{R('o', 81)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                var c = {{R('o', 85)}} is Aaaaaaaaaaaaa // fuzz
                                        or Bbbbbbbbbbbbb;
                                                bool c = {{R('o', 78)}} is Aaaaaaaaaaaaa // fuzz
                                        {{Long17}}
                                                bool c = {{R('o', 80)}} is Aaaaaaaaaaaaa // fuzz
                                        {{Long17}}
                                                bool c = {{R('o', 84)}} is Aaaaaaaaaaaaa // fuzz
                                        {{Long17}}
                                            }
                                        }
                                        """;

    static readonly string CommentsOracle = $$"""
                                              class C {
                                                  void M() {
                                                      bool c = {{R('o', 78)}} is Aaaaaaaaaaaaa // fuzz
                                                          or Bbbbbbbbbbbbb;
                                                      bool c =
                                                          {{R('o', 80)}} is Aaaaaaaaaaaaa // fuzz
                                                              or Bbbbbbbbbbbbb;
                                                      bool c =
                                                          {{R('o', 84)}} is
                                                              Aaaaaaaaaaaaa // fuzz
                                                              or Bbbbbbbbbbbbb;
                                                      var c = {{R('o', 79)}} is Aaaaaaaaaaaaa // fuzz
                                                          or Bbbbbbbbbbbbb;
                                                      var c =
                                                          {{R('o', 81)}} is Aaaaaaaaaaaaa // fuzz
                                                              or Bbbbbbbbbbbbb;
                                                      var c =
                                                          {{R('o', 85)}} is
                                                              Aaaaaaaaaaaaa // fuzz
                                                              or Bbbbbbbbbbbbb;
                                                      bool c = {{R('o', 78)}} is Aaaaaaaaaaaaa // fuzz
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB
                                                          or BBBBBBBBBBBBB;
                                                      bool c =
                                                          {{R('o', 80)}} is Aaaaaaaaaaaaa // fuzz
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB;
                                                      bool c =
                                                          {{R('o', 84)}} is
                                                              Aaaaaaaaaaaaa // fuzz
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB
                                                              or BBBBBBBBBBBBB;
                                                  }
                                              }
                                              """;

    [Fact]
    public void TheMinimisedCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Equal(MinimisedOracle + "\n", first);
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     A typed and a <c>var</c> local, operands of 75 to 105 columns, a first alternative of 5 or 30 and two
    ///     or seven alternatives: the rows the oracle and Skala now agree on, which are every row whose first
    ///     alternative ends past the margin.
    /// </summary>
    /// <remarks>
    ///     ⚠ Left out, stable and not this fix's: an operand so long that <c>operand is</c> does not fit below
    ///     the <c>=</c> either (the oracle breaks before the <c>is</c>), and #446's two-alternative table rows
    ///     with a five-column first alternative.
    /// </remarks>
    [Fact]
    public void AFirstAlternativePastTheMargin_BreaksTheEquals() {
        var formatted = FormatWith(Grid);
        Assert.Equal(GridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    /// <summary>
    ///     A line comment after the first alternative ends the line: the <c>=</c> breaks when the line through
    ///     the comment overflows (122 and 126), and stays at exactly 120, whatever follows the comment.
    /// </summary>
    [Fact]
    public void ALineCommentInThePattern_EndsTheLineItIsMeasuredBy() {
        var formatted = FormatWith(Comments);
        Assert.Equal(CommentsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
