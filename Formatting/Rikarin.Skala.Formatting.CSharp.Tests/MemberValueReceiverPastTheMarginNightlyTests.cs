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
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = """
                               internal sealed struct T248 : IDisposable {
                                public TimeSpan M273<T270, T271, T272>(Lazy<Dictionary<Span<bool>, List<Span<bool>>>> p274, decimal p275, ValueTask<bool> p276, Dictionary<Task<object>, List<Task<object>>> p277) where T270 : class {
                                for (var i278
                             = 0; i278 < (new { Name = 0x47d, Count = 'c' }, ((Nullable<Dictionary<DateTime, List<DateTime>>>)1.5d)); i278++) {
                                Dictionary<Dictionary<string, IEnumerable<object>>, List<Dictionary<string, IEnumerable<object>>>> v279 = context.First;
                               }
                               }
                               }
                             """;

    const string Grid = """
                        class T {
                            void M() {
                                for (;;) {
                                    Tyyyyyyyyyyyyyyy v = context.First;
                                    Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                    Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                }
                            }
                        }
                        """;

    const string GridOracle = """
                              class T {
                                  void M() {
                                      for (;;) {
                                          Tyyyyyyyyyyyyyyy v = context.First;
                                          Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                              .Ccccccccccccccccc;
                                          Tyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                              .Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                              .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                              .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.First;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                              .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v = context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                              .Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.First;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                                  .Ccccccccccccccccccccccccccc.Ddddddddddddddddddddd;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.First;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.Ccccccccccccccccc;
                                          Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy v =
                                              context.Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
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
