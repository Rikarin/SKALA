using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A value behind a deconstruction's or a tuple's head the author broke continues one level past the statement,
///     not past the head's last line (#613).
/// </summary>
/// <remarks>
///     ⚠ <c>var (a71,</c> / <c>b72) = source.OrderBy.F…</c> / <c>.Value;</c>: the oracle puts the dot at 12, where
///     Skala put it at 16 — and a chopped call's arguments, a ternary's branches likewise. A tuple the author broke
///     only before its <c>)</c> keeps the deeper level (the fifth row), and a binary value keeps its operators on the
///     <c>=</c>'s level (the last). Expected output is the oracle's, measured 2026-10-10 with <c>Testing ask</c>.
/// </remarks>
public sealed class DeconstructionValueLevelIssue613Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class C {
                              void M() {
                                  var (a71, b72
                                      ) = source.OrderBy.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF.Value;
                                  var (a71,
                                      b72) = Compute(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                                  (int a71,
                                      int b72) = source.OrderBy.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF.Value;
                                  (a71,
                                      b72) = cccccccccccccccccccccccccccccccccccccccccccccccc ? ddddddddddddddddddddddddddddddddddddddddddddddddd : e;
                                  (int a71, int b72
                                      ) = Compute(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                                  var (a71, b72) = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                              }
                          }
                          """;

    const string Oracle = """
                          class C {
                              void M() {
                                  var (a71, b72
                                      ) = source.OrderBy.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
                                      .Value;
                                  var (a71,
                                      b72) = Compute(
                                      aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                                      bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                  );
                                  (int a71,
                                      int b72) = source.OrderBy.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF
                                      .Value;
                                  (a71,
                                      b72) = cccccccccccccccccccccccccccccccccccccccccccccccc
                                      ? ddddddddddddddddddddddddddddddddddddddddddddddddd
                                      : e;
                                  (int a71, int b72
                                      ) = Compute(
                                          aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                                          bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                                      );
                                  var (a71, b72) = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                      + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                              }
                          }
                          """;

    [Fact]
    public void AValueBehindABrokenTupleHead_ContinuesFromTheStatement() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
