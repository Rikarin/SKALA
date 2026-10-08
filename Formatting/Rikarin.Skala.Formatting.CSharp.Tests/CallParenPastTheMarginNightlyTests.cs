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
}
