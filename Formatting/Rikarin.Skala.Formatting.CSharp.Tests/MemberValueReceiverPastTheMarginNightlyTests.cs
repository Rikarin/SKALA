using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a plain member access whose receiver does not fit beside it breaks, on both passes
///     (Nightly <c>fuzz --seed=909</c>, case 9552816164132777654).
/// </summary>
/// <remarks>
///     ⚠ The <c>=</c> yielded to the member-access fill (#482), which kept <c>T v = context</c> past the margin
///     and broke at the dot; pass two read that break as the author's and broke the <c>=</c>. The grid rows
///     are the oracle's own answers, measured 2026-10-09 with <c>Testing ask</c>: a receiver ending at
///     column 121 or later breaks the <c>=</c>, one ending at 80 or earlier keeps it. ⚠ Between, the oracle
///     breaks the <c>=</c> for a value that fits below from a receiver ending near column 100, where Skala
///     still yields to the fill — a stable divergence of #482's, not this fix's, and left out.
/// </remarks>
public sealed class MemberValueReceiverPastTheMarginNightlyTests {
    const string Long9 = "Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;";

    const string Long10 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;";

    const string Long11 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaa"
        + "aaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;";

    const string Long12 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;";

    const string Long13 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;";

    const string Long1 = "public TimeSpan M273<T270, T271, T272>(Lazy<Dictionary<Span<bool>, List<Span<boo"
        + "l>>>> p274, decimal p275, ValueTask<bool> p276, Dictionary<Task<object>, List<Ta"
        + "sk<object>>> p277) where T270 : class {";

    const string Long2 = "= 0; i278 < (new { Name = 0x47d, Count = 'c' }, ((Nullable<Dictionary<DateTime, "
        + "List<DateTime>>>)1.5d)); i278++) {";

    const string Long3 = "Dictionary<Dictionary<string, IEnumerable<object>>, List<Dictionary<string, IEnu"
        + "merable<object>>>> v279 = context.First;";

    const string Long4 = "Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;";

    const string Long5 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddd"
        + "dddddddd;";

    const string Long6 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaa"
        + "aaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccc"
        + "cccccc.Ddddddddddddddddddddd;";

    const string Long7 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;";

    const string Long8 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = $$"""
                                 internal sealed struct T248 : IDisposable {
                                  {{Long1}}
                                  for (var i278
                               {{Long2}}
                                  {{Long3}}
                                 }
                                 }
                                 }
                               """;

    static readonly string Grid = $$"""
                                    class T {
                                        void M() {
                                            for (;;) {
                                                Tyyyyyyyyyyyyyyy v = context.First;
                                                {{Long9}}
                                                {{Long4}}
                                                Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                                {{Long10}}
                                                {{Long5}}
                                                T{{R('y', 55)}} v = context.First;
                                                {{Long11}}
                                                {{Long6}}
                                                T{{R('y', 96)}} v = context.First;
                                                {{Long12}}
                                                {{Long7}}
                                                T{{R('y', 98)}} v = context.First;
                                                {{Long13}}
                                                {{Long8}}
                                            }
                                        }
                                    }
                                    """;

    static readonly string GridOracle = $$"""
                                          class T {
                                              void M() {
                                                  for (;;) {
                                                      Tyyyyyyyyyyyyyyy v = context.First;
                                                      Tyyyyyyyyyyyyyyy v = context.A{{R('a', 35)}}.B{{R('b', 31)}}
                                                          .Ccccccccccccccccc;
                                                      Tyyyyyyyyyyyyyyy v = context.A{{R('a', 35)}}.B{{R('b', 31)}}
                                                          .Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                                      Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                                      T{{R('y', 35)}} v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                          .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                                      T{{R('y', 35)}} v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                          .B{{R('b', 31)}}.C{{R('c', 26)}}.Ddddddddddddddddddddd;
                                                      T{{R('y', 55)}} v = context.First;
                                                      T{{R('y', 55)}} v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                          .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                                      T{{R('y', 55)}} v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                          .B{{R('b', 31)}}.C{{R('c', 26)}}.Ddddddddddddddddddddd;
                                                      T{{R('y', 96)}} v =
                                                          context.First;
                                                      T{{R('y', 96)}} v =
                                                          context.A{{R('a', 35)}}.B{{R('b', 31)}}.Ccccccccccccccccc;
                                                      T{{R('y', 96)}} v =
                                                          context.A{{R('a', 35)}}.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                                              .Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                                      T{{R('y', 98)}} v =
                                                          context.First;
                                                      T{{R('y', 98)}} v =
                                                          context.A{{R('a', 35)}}.B{{R('b', 31)}}.Ccccccccccccccccc;
                                                      T{{R('y', 98)}} v =
                                                          context.A{{R('a', 35)}}.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                                              .Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                                  }
                                              }
                                          }
                                          """;

    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        var first = FormatWith(Minimised);
        Assert.Equal(first, FormatWith(first));
        Assert.Contains("v279 =\n", first, StringComparison.Ordinal);
    }

    [Fact]
    public void AReceiverPastTheMargin_BreaksTheEquals() {
        var formatted = FormatWith(Grid);
        Assert.Equal(GridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
