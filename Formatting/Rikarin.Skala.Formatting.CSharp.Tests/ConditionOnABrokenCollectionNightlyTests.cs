using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a conditional whose condition is a collection broken after its <c>[</c> stays, and the
///     bracket keeps the break (Nightly <c>fuzz --seed=7777</c>, case 11325995557757886152).
/// </summary>
/// <remarks>
///     ⚠ Skala broke the <c>=</c> there. The fuzzer reached it through a first pass that kept the author's
///     break before a comma, <c>[ 'c'</c> / <c>, true]</c>, as a break after the <c>[</c> — a stable divergence
///     of its own, the oracle joins it — and a second pass that then broke the <c>=</c>. Expected outputs are
///     the oracle's, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class ConditionOnABrokenCollectionNightlyTests {
    const string Long3 = "public static readonly Dictionary<CancellationToken, List<CancellationToken>> F1"
        + "7 = [ 'c'";

    const string Long1 = """, true, true,  3_000_000L, 36269] ? ['c', "sss", "sss", "utf8"u8] : typeof(Cance"""
        + "llationToken);";

    const string Long2 = "eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee] ? [1, 2] : typeof(Ca"
        + "ncellationToken);";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = $$"""
                               namespace Fuzz.N1  {
                                 public class T16 {
                                 {{Long3}}
                               {{Long1}}
                                 }
                               }
                               """;

    static readonly string Grid = $$"""
                                    class C {
                                        void M() {
                                            object nnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnn = [
                                    {{Long2}}
                                            object nnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    {{Long2}}
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    {{Long2}}
                                            object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                    eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 70)}} = [
                                    eeeeeeee, eeeeeeee] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 70)}} = [
                                    eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 70)}} = [
                                    {{Long2}}
                                            object {{R('n', 70)}} = [
                                    eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 85)}} = [
                                    eeeeeeee, eeeeeeee] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 85)}} = [
                                    eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                            object {{R('n', 85)}} = [
                                    {{Long2}}
                                            object {{R('n', 85)}} = [
                                    eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                    ] ? [1, 2] : typeof(CancellationToken);
                                        }
                                    }
                                    """;

    static readonly string GridOracle = $$"""
                                          class C {
                                              void M() {
                                                  object nnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 70)}} = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 70)}} = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 70)}} = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 70)}} = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 85)}} = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 85)}} = [
                                                      eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 85)}} = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                                  object {{R('n', 85)}} = [
                                                      eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee, eeeeeeee
                                                  ]
                                                      ? [1, 2]
                                                      : typeof(CancellationToken);
                                              }
                                          }
                                          """;

    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Equal(first, FormatWith(first));
        Assert.Contains("F17 = [\n", first, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A local, heads of 10 to 85 columns, elements of 20 and 60, the collection broken after its <c>[</c>
    ///     alone or before its <c>]</c> too: the <c>=</c> stays wherever the statement does not fit.
    /// </summary>
    [Fact]
    public void ABrokenCollectionCondition_KeepsTheEquals() {
        var formatted = FormatWith(Grid);
        Assert.Equal(GridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
