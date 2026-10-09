using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A conditional's call condition whose <c>(</c> would end past the margin beside the <c>=</c> breaks the
///     <c>=</c> (Nightly fuzz, case 1701945859786365053, #594).
/// </summary>
/// <remarks>
///     ⚠ A call condition that fits nowhere behind an <c>=</c> past column 40 keeps the <c>=</c> and chops its
///     arguments (#553). Where the call's <c>(</c> itself ran past the margin beside the <c>=</c>, pass one kept
///     the <c>=</c> all the same and wrote the <c>(</c> past the margin, or broke a type argument list after its
///     <c>&lt;</c>; pass two, finding the condition broken, broke the <c>=</c>. The oracle breaks it on both
///     inputs, at exactly 121: the <c>(</c> ending at column 120 keeps the <c>=</c>, at 121 breaks it, generic
///     or plain callee alike. Expected output is the oracle's, measured 2026-10-10 with <c>Testing ask</c>.
/// </remarks>
public sealed class ConditionalCallPastTheMarginNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
        class C {
            void M() {
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v025 = Materialise<Task<List<byte>>, ValueTask<DateTime>>(out var o102, (56846 ? @"verbatim\path" : 42592), source?.Items?.Count) ? "sss" : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v019 = Materialise<YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY>(out var o102, (56846 ? @"verbatim\path" : 42592), source?.Items?.Count) ? "sss" : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v020 = MaterialiseZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ(out var o102, (56846 ? @"verbatim\path" : 42592), source?.Items?.Count) ? "sss" : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v017 = Materialise<YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY>(out var o102, (56846 ? @"verbatim\path" : 42592), source?.Items?.Count) ? "sss" : "ttt";
            }
        }
        """;

    const string Oracle = """
        class C {
            void M() {
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, int> v025 =
                    Materialise<Task<List<byte>>, ValueTask<DateTime>>(
                        out var o102,
                        (56846 ? @"verbatim\path" : 42592),
                        source?.Items?.Count
                    )
                        ? "sss"
                        : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v019 =
                    Materialise<YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY>(
                        out var o102,
                        (56846 ? @"verbatim\path" : 42592),
                        source?.Items?.Count
                    )
                        ? "sss"
                        : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v020 =
                    MaterialiseZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ(
                        out var o102,
                        (56846 ? @"verbatim\path" : 42592),
                        source?.Items?.Count
                    )
                        ? "sss"
                        : "ttt";
                Dictionary<XXXXXXXXXXXXXXXXXXXXXXX, int> v017 = Materialise<YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY>(
                    out var o102,
                    (56846 ? @"verbatim\path" : 42592),
                    source?.Items?.Count
                )
                    ? "sss"
                    : "ttt";
            }
        }
        """;

    [Fact]
    public void ACallConditionWhoseParenRunsPastTheMargin_BreaksTheEquals() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
