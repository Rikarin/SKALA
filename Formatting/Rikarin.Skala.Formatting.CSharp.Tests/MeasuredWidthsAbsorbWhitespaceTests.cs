namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The widths the measured tables are keyed on are the formatted ones, not the source's: whitespace the
///     formatter absorbs must not move a table to another row (<c>format(mutate(x)) ≡ format(x)</c>,
///     docs/plan/12 § "Fuzzing").
/// </summary>
/// <remarks>
///     ⚠ The Nightly fuzzer's seed 37583856628 (replay 6225963390046177533, origin
///     <c>constructs/breaks/lambda-parameters-one-column-over.cs</c>) found it. #572's one-column table and
///     #583's type/name break read the declaration's type off its source span, so `Func <TTTT >` measured four
///     columns wider than `Func&lt;TTTT&gt;` and the line broke before the name instead of chopping the
///     parameters. Every width behind #557, #558, #571, #572, #578 and #528's held call is now
///     <c>BreakPlan.FormattedWidth</c>.
/// </remarks>
public sealed class MeasuredWidthsAbsorbWhitespaceTests {
    static string P(int width) => new('P', width);

    static string Wrap(string indent, string line) => $$"""
        namespace P;

        class C {
        {{indent}}void M() {
        {{indent}}{{indent}}{{line}}
        {{indent}}}
        }
        """;

    /// <summary>The fuzzer's minimised case: a two-space indent is absorbed like a four-space one.</summary>
    [Fact]
    public void TheMinimisedCase_FormatsAsTheCleanOne() {
        var line = $"Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffffffffffffffff = ({P(48)} p0) => v;";
        var clean = Format.Text(Wrap("    ", line));
        Assert.Equal(clean, Format.Text(Wrap("  ", line)));
        Assert.Contains("ffffffffffffffffffffffff = (\n", clean, StringComparison.Ordinal);
        Assert.Equal(clean, Format.Text(clean));
    }

    /// <summary>
    ///     ⚠ The fuzzer's own mutated line: gaps inside the type and the parameter list, widened around the
    ///     `=&gt;`, trailing spaces. The widths the tables read come out as the formatter writes them.
    /// </summary>
    [Fact]
    public void WidenedGapsInsideTheTypeAndTheParameters_DoNotMoveTheTable() {
        var clean = Format.Text(
            Wrap("    ", $"Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffffffffffffffff = ({P(48)} p0) => v;")
        );
        var widened = Format.Text(
            Wrap("    ", $"Func <TTTTTTTTTTTTTTTTTTTT > ffffffffffffffffffffffff = ( {P(48)} p0)    =>   v;   ")
        );
        Assert.Equal(clean, widened);
    }

    /// <summary>
    ///     #558's name gate, #557's lambda head and #578's parameter text, each with gaps the formatter
    ///     removes: spaces inside the type's angle brackets and the parameter list, and widened gaps around
    ///     the `=`, the `=&gt;` and the `;`.
    /// </summary>
    [Theory]
    [InlineData("Func<A, B> fffffffffffffffffffffffffffffffffffff = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => vvvvvvvvvvvv;")]
    [InlineData("Use((Tttttttttt first, U second) => first.Alpha.Bravo.Charlie.Delta.Echo.Foxtrot.Golf.Hotel.India.Juliett.Kilo.Lima.Mike.November);")]
    [InlineData("UUUUUUUUUUUU(static nnnnnnnn => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccc);")]
    public void WidenedGaps_FormatAsTheCleanLine(string clean) {
        var widened = clean.Replace("<", " < ")
            .Replace(">", " > ")
            .Replace(" < A,", " < A ,")
            .Replace("((", "( (  ")
            .Replace("= (", "  =  ( ")
            .Replace(" p0)", "   p0 )")
            .Replace(" second)", "   second )")
            .Replace(" = > ", "   =>   ")
            .Replace(" && ", "   &&  ")
            .Replace(");", " )  ;")
            .Replace("(static ", "(  static   ");
        Assert.NotEqual(clean, widened);
        Assert.Equal(Format.Text(Wrap("    ", clean)), Format.Text(Wrap("    ", widened)));
    }

    /// <summary>
    ///     ⚠ Nightly fuzzer, seed 1 (replay 13096041111892358404, Newtonsoft's ConstructorHandlingTests.cs): a
    ///     space before a type argument list's <c>&gt;</c> that the formatter removes moved #528's held-value
    ///     table, read through <c>BreakPlan.FlatSourceWidth</c>.
    /// </summary>
    [Fact]
    public void ASpaceInsideATypeArgumentList_DoesNotMoveTheHeldValue() {
        const string Name = "PublicParameterizedConstructorWithNonPropertyParameterTestClass";
        static string Source(string close) => $$"""
            namespace Newtonsoft.Json.Tests.Serialization
            {
              public class ConstructorHandlingTests : TestFixtureBase
              {
              public void SuccessWithPublicParameterizedConstructorWhenParameterIsNotAProperty()
              {
               {{Name}} c = JsonConvert.DeserializeObject<{{Name}}{{close}}(json);
              }
              }
            }
            """;
        Assert.Equal(Format.Text(Source(">")), Format.Text(Source(" >")));
    }

    /// <summary>
    ///     ⚠ Nightly fuzzer, seeds 4304693669410283359 and 17091299203163347117 (idempotency): with the call's
    ///     <c>(</c> past the margin the first pass kept <c>= Emit(</c> on a 123-column line and chopped the
    ///     arguments, and the second, reading them as broken, moved the call below the <c>=</c>. The <c>=</c>
    ///     breaks on the first pass now.
    /// </summary>
    [Fact]
    public void AnEqualsBeforeACallWhoseParenIsPastTheMargin_IsStableOnTheSecondPass() {
        const string Source = """""
            internal sealed readonly struct T2<T3> {
                private ImmutableArray<((double? First, long Second) First, (CancellationToken First, long Second) Second)> f23 = Emit(Materialise<TimeSpan>($"value {97} and {items[0]}", x24 => source?.Value?.Length, (state is null)), state, """"a { b } c"""");
            }
            """"";
        var once = Format.Text(Source);
        Assert.Equal(once, Format.Text(once));
        Assert.Contains(" f23 =\n", once, StringComparison.Ordinal);
    }
}
