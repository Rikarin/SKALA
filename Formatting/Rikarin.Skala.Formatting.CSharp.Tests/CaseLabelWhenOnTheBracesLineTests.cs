using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A case label's <c>when</c> written on the line of its property pattern's <c>}</c> stays there once the braces
///     broke open, and a type argument list after it fills on that line (fuzz 16251829570492293429, SK-DIV-0552).
/// </summary>
/// <remarks>
///     ⚠ Skala moved the <c>when</c> below for its head through the call's <c>(</c>, and pass two, reading that break as
///     the author's, lifted the braces (#575). Expected outputs are the oracle's, measured 2026-10-14 with
///     <c>Testing ask</c>: the braces broken by a kept break after a subpattern's colon and by width alike.
/// </remarks>
public sealed class CaseLabelWhenOnTheBracesLineTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    [Fact]
    public void TheWhenStaysOnTheBracesLine_AndItsTypeArgumentsFill() {
        var source = """
                     public class C {
                         void M5() {
                             switch (o) {
                                 case { Length:
                     > 0 } when ready:
                                     break;
                             }
                         }

                         void M6() {
                             switch (o) {
                                 case { Length:
                     > 0 } when Convert<StringBuilder, Nullable<StringBuilder>, CancellationToken, StringBuilder, Nullable<StringBuilder>>(x47, bbbbbbbbbbbbbbbbbbbbbbbbbbbb):
                                     break;
                             }
                         }

                         void M26() {
                             switch (o) {
                                 case { Length: > 0, Name: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Other: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" } when Convert<StringBuilder, Nullable<StringBuilder>, CancellationToken, StringBuilder, Nullable<StringBuilder>>(x47, bbbbbbbbbbbbbbbbbbbbbbbbbbbb):
                                     break;
                             }
                         }
                     }
                     """;
        var oracle = """
                     public class C {
                         void M5() {
                             switch (o) {
                                 case {
                                     Length:
                                     > 0
                                 } when ready:
                                     break;
                             }
                         }

                         void M6() {
                             switch (o) {
                                 case {
                                     Length:
                                     > 0
                                 } when Convert<StringBuilder, Nullable<StringBuilder>, CancellationToken, StringBuilder,
                                     Nullable<StringBuilder>>(x47, bbbbbbbbbbbbbbbbbbbbbbbbbbbb):
                                     break;
                             }
                         }

                         void M26() {
                             switch (o) {
                                 case {
                                     Length: > 0,
                                     Name: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                     Other: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                                 } when Convert<StringBuilder, Nullable<StringBuilder>, CancellationToken, StringBuilder,
                                     Nullable<StringBuilder>>(x47, bbbbbbbbbbbbbbbbbbbbbbbbbbbb):
                                     break;
                             }
                         }
                     }

                     """;

        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }
}
