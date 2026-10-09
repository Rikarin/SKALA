using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

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
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = """
                             class EqualsBeforeABinaryPattern {
                               void M() { // fuzz
                               bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                             or Bbbbbbbbbbbbb; // fuzz
                               }
                             }
                             """;

    const string MinimisedOracle = """
                                   class EqualsBeforeABinaryPattern {
                                       void M() { // fuzz
                                           bool c =
                                               oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                                   or Bbbbbbbbbbbbb; // fuzz
                                       }
                                   }
                                   """;

    const string Grid = """
                        class C {
                            void M() {
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB or BBBBBBBBBBBBBBBBBBBB;
                            }
                        }
                        """;

    const string GridOracle = """
                              class C {
                                  void M() {
                                      bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      bool c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                          or BBBBBBBBBBBBBBBBBBBB;
                                      var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is AAAAA
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB
                                          or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAA
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB
                                              or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                              AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB;
                                      var c =
                                          ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
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

    const string Comments = """
                            class C {
                                void M() {
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or Bbbbbbbbbbbbb;
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB;
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB;
                                    bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                            or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB or BBBBBBBBBBBBB;
                                }
                            }
                            """;

    const string CommentsOracle = """
                                  class C {
                                      void M() {
                                          bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                              or Bbbbbbbbbbbbb;
                                          bool c =
                                              oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                                  or Bbbbbbbbbbbbb;
                                          bool c =
                                              oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                                  Aaaaaaaaaaaaa // fuzz
                                                  or Bbbbbbbbbbbbb;
                                          var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                              or Bbbbbbbbbbbbb;
                                          var c =
                                              ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                                  or Bbbbbbbbbbbbb;
                                          var c =
                                              ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                                                  Aaaaaaaaaaaaa // fuzz
                                                  or Bbbbbbbbbbbbb;
                                          bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB
                                              or BBBBBBBBBBBBB;
                                          bool c =
                                              oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa // fuzz
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB
                                                  or BBBBBBBBBBBBB;
                                          bool c =
                                              oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
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
