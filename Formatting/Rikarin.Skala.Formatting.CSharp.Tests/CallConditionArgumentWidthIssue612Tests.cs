using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A conditional's call condition that fits below its <c>=</c>: past a ten-column callee, the oracle's choice
///     between breaking the <c>=</c> and chopping the call follows the arguments' width and the branches' (#612).
/// </summary>
/// <remarks>
///     ⚠ #596's fit reads the argument count, which its probe's long arguments made a stand-in for their width.
///     Rows from the fresh probe (2026-10-10, <c>Testing ask</c>): the same call chops behind short branches and
///     moves down behind long ones (the first two), and a single wide argument moves down where #596's fit chopped
///     it (the last two). Expected output is the oracle's.
/// </remarks>
public sealed class CallConditionArgumentWidthIssue612Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class C {
                              void M() {
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v064 = SelectZZZZZ(aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccc) ? "sss" : "ttt";
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v400 = SelectZZZZZ(aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccc) ? Cast<object>(first, second) : context;
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v165 = SelectZZZZZZZZZZZZZZZZZ(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) ? "sss" : "ttt";
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v410 = SelectZZZZZZZZZZZZZZZZZ(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccccccccccc) ? Cast<object>(first, second) : context;
                              }
                          }
                          """;

    const string Oracle = """
                          class C {
                              void M() {
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v064 = SelectZZZZZ(
                                      aaaaaaaaaaaaaaaaaaaaaaa,
                                      bbbbbbbbbbbbbbbbbbbbbbb,
                                      ccccccccccccccccccccccccc
                                  )
                                      ? "sss"
                                      : "ttt";
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v400 =
                                      SelectZZZZZ(aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccc)
                                          ? Cast<object>(first, second)
                                          : context;
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v165 =
                                      SelectZZZZZZZZZZZZZZZZZ(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) ? "sss" : "ttt";
                                  Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v410 =
                                      SelectZZZZZZZZZZZZZZZZZ(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccccccccccc)
                                          ? Cast<object>(first, second)
                                          : context;
                              }
                          }
                          """;

    [Fact]
    public void ACallConditionPastATenColumnCallee_ChopsByItsArgumentsAndBranches() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
