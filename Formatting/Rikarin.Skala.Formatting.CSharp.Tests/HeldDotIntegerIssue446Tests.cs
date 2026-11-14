namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ A held one-argument call whose <c>(</c> lands past the margin beside a kept <c>=</c> breaks at its dot by one
///     integer rule, <c>38·argument − receiver + 57·max(0, 115 − bare) ≤ max(428 − 7·name, 14·name − 34)</c> (#446,
///     SK-DIV-0211 round 10). Each pair is the oracle's last dot and the next argument up, at indent 8; every expected
///     string is <c>jb cleanupcode</c>'s. With round 9's two fitted lines put back, 3 of the 8 fail.
/// </summary>
public sealed class HeldDotIntegerIssue446Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static string In(params string[] lines) {
        var text = string.Empty;
        foreach (var line in lines) {
            text += "        " + line + "\n";
        }

        return text;
    }

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    static string N(int n) => "n" + new string('m', n - 1);

    static string R(int n) => new('r', n);

    static string A(int n) => new('a', n);

    static string T(int n) => "T" + new string('t', n - 1);

    static string M(int n) => "M" + new string('m', n - 1);

    /// <summary>
    ///     The cap: an assignment of 12, a receiver of 88, a method of 12, an argument of 11 takes the dot. Round 9's
    ///     cap line stopped at 10.
    /// </summary>
    [Fact]
    public void Cap_Name12_ArgumentOf11_TakesTheDot() =>
        Agrees(
            In($"{N(12)} = {R(88)}.{M(12)}({A(11)});"),
            In($"{N(12)} = {R(88)}", $"    .{M(12)}({A(11)});")
        );

    /// <summary>And 12 breaks the <c>=</c> and chops below.</summary>
    [Fact]
    public void Cap_Name12_ArgumentOf12_BreaksBoth() =>
        Agrees(
            In($"{N(12)} = {R(88)}.{M(12)}({A(12)});"),
            In($"{N(12)} =", $"    {R(88)}.{M(12)}(", $"        {A(12)}", "    );")
        );

    /// <summary>
    ///     The rising edge: <c>var</c> and a name of 10, the bare line below ending at 112, an argument of 7 takes the
    ///     dot. Round 9's line of a column and a half moved it below.
    /// </summary>
    [Fact]
    public void Rise_Bare112_ArgumentOf7_TakesTheDot() =>
        Agrees(
            In($"var {N(10)} = {R(84)}.{M(12)}({A(7)});"),
            In($"var {N(10)} = {R(84)}", $"    .{M(12)}({A(7)});")
        );

    /// <summary>And 8 moves the value below whole.</summary>
    [Fact]
    public void Rise_Bare112_ArgumentOf8_MovesBelow() =>
        Agrees(
            In($"var {N(10)} = {R(84)}.{M(12)}({A(8)});"),
            In($"var {N(10)} =", $"    {R(84)}.{M(12)}({A(8)});")
        );

    /// <summary>A typed local, a type of 6 and a name of 16, a method of 22: an argument of 5 takes the dot.</summary>
    [Fact]
    public void Rise_Typed_ArgumentOf5_TakesTheDot() =>
        Agrees(
            In($"{T(6)} {N(16)} = {R(74)}.{M(22)}({A(5)});"),
            In($"{T(6)} {N(16)} = {R(74)}", $"    .{M(22)}({A(5)});")
        );

    /// <summary>And 6 moves the value below whole, where round 9 kept the dot.</summary>
    [Fact]
    public void Rise_Typed_ArgumentOf6_MovesBelow() =>
        Agrees(
            In($"{T(6)} {N(16)} = {R(74)}.{M(22)}({A(6)});"),
            In($"{T(6)} {N(16)} =", $"    {R(74)}.{M(22)}({A(6)});")
        );

    /// <summary>
    ///     ⚠ The name's V: behind a name of 38 the cap rises again, to an argument of 14 with a receiver of 58. Round 9's
    ///     cap fell with the name and broke the <c>=</c> from 5.
    /// </summary>
    [Fact]
    public void LongName_ArgumentOf14_TakesTheDot() =>
        Agrees(
            In($"var {N(38)} = {R(58)}.{M(44)}({A(14)});"),
            In($"var {N(38)} = {R(58)}", $"    .{M(44)}({A(14)});")
        );

    /// <summary>And 15 breaks the <c>=</c> and chops below.</summary>
    [Fact]
    public void LongName_ArgumentOf15_BreaksBoth() =>
        Agrees(
            In($"var {N(38)} = {R(58)}.{M(44)}({A(15)});"),
            In($"var {N(38)} =", $"    {R(58)}.{M(44)}(", $"        {A(15)}", "    );")
        );
}
