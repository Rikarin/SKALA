using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a call whose <c>(</c> falls past the margin breaks, on both passes (Nightly
///     <c>fuzz --seed=37583856628</c>, case 4304693669410283359).
/// </summary>
/// <remarks>
///     ⚠ Pass one kept <c>T v = Select(</c> by EqualsFloor's table, extrapolated past the 112 columns it was
///     measured to, and chopped the arguments; pass two read the chop as the author's, lost the floor and
///     broke the <c>=</c> — which is the oracle's answer for both inputs. The grid rows are the oracle's own
///     answers, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CallParenPastTheMarginNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = """
                             public record struct T1(byte p2 = default, decimal p3 = default, long p4 = default) {
                               protected virtual CancellationToken M9<T8>() {
                               if (state is Nullable<CancellationToken> { P36: [_, .. var rest38] }) {
                                if (value switch { not (0 or 1 or 2) when TryGet() => @"verbatim\path", { Length: > 0 } => ((x, y) => {
                              {TAB}{TAB}{TAB}{TAB}{TAB}{TAB}}), _ => (x => null) }) {
                              {TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}IReadOnlyList<Dictionary<Dictionary<string, TimeSpan>, List<   Dictionary<string, TimeSpan>>>> v148 = Select(new char() { P149 = "ss", P150 = 1.5d, P151 = 41022, P152 = "sss" }, name153: 36713, name154: (state is [_, .. var rest156]), name159: x160 => this);
                                }
                               }
                               }
                              }
                             """;

    const string Grid = """
                        class T {
                            void M() {
                                if (a) {
                                    if (b) {
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, argument5, argument6, argument7, argument8, argument9, argument10, argument11, argument12, x);
                                    }
                                }
                            }
                        }
                        """;

    const string GridOracle = """
                              class T {
                                  void M() {
                                      if (a) {
                                          if (b) {
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v148 =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(argument0, argument1, argument2, argument3, argument4, argument5, x);
                                              var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =
                                                  Select(
                                                      argument0,
                                                      argument1,
                                                      argument2,
                                                      argument3,
                                                      argument4,
                                                      argument5,
                                                      argument6,
                                                      argument7,
                                                      argument8,
                                                      argument9,
                                                      argument10,
                                                      argument11,
                                                      argument12,
                                                      x
                                                  );
                                          }
                                      }
                                  }
                              }
                              """;

    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        var first = FormatWith(Minimised.Replace("{TAB}", "\t", StringComparison.Ordinal));
        Assert.Equal(first, FormatWith(first));
        Assert.Contains("v148 =\n", first, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A typed and a <c>var</c> local, arguments of 20, 60 and 140 columns, the <c>(</c> at 121 to 127:
    ///     every one breaks the <c>=</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The rows with the <c>(</c> at 108 to 120 are left out. With 20 columns of arguments there the
    ///     oracle keeps a typed local's <c>=</c> and chops, and Skala breaks it — a stable divergence in
    ///     EqualsFloor's typed rows, not this fix's.
    /// </remarks>
    [Fact]
    public void AParenPastTheMargin_BreaksTheEquals() {
        var formatted = FormatWith(Grid);
        Assert.Equal(GridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    // ⚠ Arguments holding a certain break: a switch expression or a block-bodied lambda has no flat width,
    // and the rule returned Flat before it read the `(` (Nightly `fuzz --seed=4242`, case
    // 7862808234978504853). `<SP>` is a trailing space, as the fuzzer left it.
    const string CertainMinimised = """
                                    public sealed struct T2<T3, T4, T5> : IDisposable, IEnumerable<bool?>, IComparable<T2<T3, T4, T5>> where T3 : class, new() where T4 : class, new() where T5 : struct {
                                       internal class T78 : IDisposable, IEnumerable<StringBuilder> {
                                      public static async ValueTask<bool> M83(ref string p84, IEnumerable<byte?> p85) {
                                       Func<(CancellationToken First, Task<Guid> Second), (CancellationToken First, Task<Guid> Second)> v116 = TryGet((state with  { P117 = "ss" }), x118 => Select((value is not null), (value switch { not null =>  "sss", _ => 1.0m }), [34507, 1.0m, true, 75570] , new[] { 20066, "sss", 65780, 1.0m, "sss", "s" }), Select(context.OrderBy.First(37062).OrderBy(true).Where(x119 => 94206)), Materialise<object, ValueTask<Guid>>(Compute(), (async x => await @"verbatim\path")));<SP>
                                      }
                                      }
                                    }
                                    """;

    const string CertainGrid = """
                               class C {
                                   void M() {
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                       var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });
                                   }
                               }
                               """;

    const string CertainGridOracle = """
                                     class C {
                                         void M() {
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 a,
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 },
                                                 b
                                             );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 a,
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 },
                                                 b
                                             );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 }
                                             );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 }
                                             );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 a,
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 },
                                                 b
                                             );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 a,
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 },
                                                 b
                                             );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     a,
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     },
                                                     b
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 }
                                             );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(
                                                 x switch {
                                                     1 => 2,
                                                     _ => 3
                                                 }
                                             );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(
                                                     x switch {
                                                         1 => 2,
                                                         _ => 3
                                                     }
                                                 );
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                             var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                                                 Fn(a, () => { return 1; });
                                         }
                                     }
                                     """;

    [Fact]
    public void TheCertainBreakCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(CertainMinimised.Replace("<SP>", " ", StringComparison.Ordinal));
        Assert.Contains("v116 =\n", first, StringComparison.Ordinal);
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     A typed and a <c>var</c> local before <c>Fn(</c> with a switch expression or a block-bodied lambda
    ///     among its arguments, the <c>(</c> at 119 to 124: the oracle keeps the <c>=</c> through 120 and breaks it
    ///     from 121, measured 2026-10-09 with <c>Testing ask</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Rows with the <c>(</c> at 125 or further are left out: there the <c>=</c> itself is past the margin
    ///     and the oracle also breaks a <c>var</c> local before its name — a stable divergence outside this rule.
    /// </remarks>
    [Fact]
    public void AParenPastTheMargin_BreaksTheEquals_OverACertainBreak() {
        var formatted = FormatWith(CertainGrid);
        Assert.Equal(CertainGridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
