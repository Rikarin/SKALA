namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ A single one-argument call that is a whole <c>=</c> value, its line one column past the margin (#446,
///     SK-DIV-0211 round 7): the oracle moves it below at its dot only while its <c>(</c> lands far enough right of where
///     the value starts (<c>10·paren − 3·column ≥ 871</c>); short of that it chops the argument. #528 moved the dot at
///     any <c>(</c>. Each pair is a column apart, and every expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class HeldCallOneOverIssue446Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static string In(int depth, params string[] lines) {
        var text = string.Empty;
        for (var k = 0; k < depth; k++) {
            text += new string(' ', 8 + 4 * k) + "if (b) {\n";
        }

        foreach (var line in lines) {
            text += new string(' ', 8 + 4 * depth) + line + "\n";
        }

        for (var k = depth - 1; k >= 0; k--) {
            text += new string(' ', 8 + 4 * k) + "}\n";
        }

        return text;
    }

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    static string R(int n) => new('r', n);

    static string A(int n) => new('a', n);

    /// <summary>Beside a kept <c>var</c> <c>=</c>, the <c>(</c> on column 92: the argument chops.</summary>
    [Fact]
    public void Beside_ParenOn92_Chops() =>
        Agrees(
            In(0, $"var nnnn = {R(65)}.MMMMMM({A(27)});"),
            In(0, $"var nnnn = {R(65)}.MMMMMM(", $"    {A(27)}", ");")
        );

    /// <summary>One column further right, on 93: the dot takes the break, as #528 measured.</summary>
    [Fact]
    public void Beside_ParenOn93_MovesTheDot() =>
        Agrees(
            In(0, $"var nnnn = {R(66)}.MMMMMM({A(26)});"),
            In(0, $"var nnnn = {R(66)}", $"    .MMMMMM({A(26)});")
        );

    /// <summary>At indent 24 behind a short assignment the bound is 96: the <c>(</c> on 95 chops.</summary>
    [Fact]
    public void Beside_Indent24_ParenOn95_Chops() =>
        Agrees(
            In(4, $"nn = {R(58)}.MMMMMM({A(24)});"),
            In(4, $"nn = {R(58)}.MMMMMM(", $"    {A(24)}", ");")
        );

    /// <summary>And on 96 the dot breaks.</summary>
    [Fact]
    public void Beside_Indent24_ParenOn96_MovesTheDot() =>
        Agrees(
            In(4, $"nn = {R(59)}.MMMMMM({A(23)});"),
            In(4, $"nn = {R(59)}", $"    .MMMMMM({A(23)});")
        );

    /// <summary>
    ///     Below a typed local's broken <c>=</c>, the <c>(</c> on 90: the argument chops there. ⚠ The oracle chops on 91
    ///     too; the bound, a safe one, leaves that column to #528's dot.
    /// </summary>
    [Fact]
    public void BelowABrokenEquals_ParenOn90_Chops() =>
        Agrees(
            In(0, $"{new string('T', 30)} nnnnnnnn = {R(70)}.MMMMMM({A(29)});"),
            In(0, $"{new string('T', 30)} nnnnnnnn =", $"    {R(70)}.MMMMMM(", $"        {A(29)}", "    );")
        );

    /// <summary>On 92 the dot breaks below the <c>=</c>, in the oracle and here.</summary>
    [Fact]
    public void BelowABrokenEquals_ParenOn92_MovesTheDot() =>
        Agrees(
            In(0, $"{new string('T', 30)} nnnnnnnn = {R(72)}.MMMMMM({A(27)});"),
            In(0, $"{new string('T', 30)} nnnnnnnn =", $"    {R(72)}", $"        .MMMMMM({A(27)});")
        );
}
