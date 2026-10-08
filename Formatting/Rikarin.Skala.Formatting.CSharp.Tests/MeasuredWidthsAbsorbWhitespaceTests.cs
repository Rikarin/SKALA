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
}
