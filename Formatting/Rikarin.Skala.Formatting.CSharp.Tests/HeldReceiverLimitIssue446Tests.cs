namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ An <c>=</c> before a single method call on a receiver, <c>r.M(…)</c>, whose line overflows (#446, SK-DIV-0211
///     round 9): the four-line list limit, one argument 21 columns above two, the typed gate's two-line boundaries, a
///     narrow typed head, the dot past the margin, and two arguments one column over. Each pair differs in one width
///     unless its summary says otherwise, and every expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class HeldReceiverLimitIssue446Tests {
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
    ///     Behind <c>var</c> and a name of 32, a callee of 16: a list of 55 still breaks the <c>=</c>. Round 8's
    ///     short-head line was flat in the head and broke it to 57.
    /// </summary>
    [Fact]
    public void LongHead_ShortCallee_ListOf55_BreaksTheEquals() =>
        Agrees(
            In($"var {N(32)} = {R(9)}.{M(6)}({A(10)}, {A(43)});"),
            In($"var {N(32)} =", $"    {R(9)}.{M(6)}({A(10)}, {A(43)});")
        );

    /// <summary>And a list of 56 keeps it and chops: the limit falls with the head once it passes the callee's turn.</summary>
    [Fact]
    public void LongHead_ShortCallee_ListOf56_Chops() =>
        Agrees(
            In($"var {N(32)} = {R(9)}.{M(6)}({A(10)}, {A(44)});"),
            In($"var {N(32)} = {R(9)}.{M(6)}(", $"    {A(10)},", $"    {A(44)}", ");")
        );

    /// <summary>
    ///     A typed local's gate, a callee of 12 and a name of 28: the <c>(</c> at 86 keeps the <c>=</c> and chops.
    ///     Round 8's single lines left this name no gate at all.
    /// </summary>
    [Fact]
    public void TypedGate_ParenAt86_Chops() =>
        Agrees(
            In($"{T(33)} {N(28)} = {R(5)}.{M(6)}({A(10)}, {A(22)});"),
            In($"{T(33)} {N(28)} = {R(5)}.{M(6)}(", $"    {A(10)},", $"    {A(22)}", ");")
        );

    /// <summary>And a column more of type puts the <c>(</c> at 87, right of the gate: the <c>=</c> breaks.</summary>
    [Fact]
    public void TypedGate_ParenAt87_BreaksTheEquals() =>
        Agrees(
            In($"{T(34)} {N(28)} = {R(5)}.{M(6)}({A(10)}, {A(22)});"),
            In($"{T(34)} {N(28)} =", $"    {R(5)}.{M(6)}({A(10)}, {A(22)});")
        );

    /// <summary>
    ///     One argument behind a typed head, a callee of 80, the value too wide below: an argument of 66 breaks the
    ///     <c>=</c> and chops below it (the limit, 21 columns over two arguments', is 68 here; round 8 had no such
    ///     limit for a typed head).
    /// </summary>
    [Fact]
    public void TypedOneArgument_Of66_BreaksBoth() =>
        Agrees(
            In($"{T(8)} {N(8)} = {R(73)}.{M(6)}({A(66)});"),
            In($"{T(8)} {N(8)} =", $"    {R(73)}.{M(6)}(", $"        {A(66)}", "    );")
        );

    /// <summary>
    ///     And one of 69 keeps the <c>=</c> and chops beside it, where the receiver's end past 87 broke the <c>=</c>
    ///     (three columns from the cell above).
    /// </summary>
    [Fact]
    public void TypedOneArgument_Of69_Chops() =>
        Agrees(
            In($"{T(8)} {N(8)} = {R(73)}.{M(6)}({A(69)});"),
            In($"{T(8)} {N(8)} = {R(73)}.{M(6)}(", $"    {A(69)}", ");")
        );

    /// <summary>A narrow typed head, <c>Tttt nmmm =</c>, behind a callee of 96: an argument of 28 breaks both.</summary>
    [Fact]
    public void NarrowTypedHead_Of28_BreaksBoth() =>
        Agrees(
            In($"{T(4)} {N(4)} = {R(89)}.{M(6)}({A(28)});"),
            In($"{T(4)} {N(4)} =", $"    {R(89)}.{M(6)}(", $"        {A(28)}", "    );")
        );

    /// <summary>And one of 29 keeps the <c>=</c>, as a <c>var</c> head of the same width does.</summary>
    [Fact]
    public void NarrowTypedHead_Of29_Chops() =>
        Agrees(
            In($"{T(4)} {N(4)} = {R(89)}.{M(6)}({A(29)});"),
            In($"{T(4)} {N(4)} = {R(89)}.{M(6)}(", $"    {A(29)}", ");")
        );

    /// <summary>
    ///     The <c>(</c> past the margin beside a kept <c>=</c>: a short argument (10) takes the dot, where #528's rule
    ///     moved the value below whole (two columns from the next cell).
    /// </summary>
    [Fact]
    public void ParenPastTheMargin_ShortArgument_TakesTheDot() =>
        Agrees(
            In($"var {N(12)} = {R(89)}.{M(12)}({A(10)});"),
            In($"var {N(12)} = {R(89)}", $"    .{M(12)}({A(10)});")
        );

    /// <summary>And an argument of 12 breaks the <c>=</c> and chops below, where #528's rule took the dot.</summary>
    [Fact]
    public void ParenPastTheMargin_LongerArgument_BreaksBoth() =>
        Agrees(
            In($"var {N(12)} = {R(89)}.{M(12)}({A(12)});"),
            In($"var {N(12)} =", $"    {R(89)}.{M(12)}(", $"        {A(12)}", "    );")
        );

    /// <summary>Behind a head under twelve the dot takes the line whatever the argument: 40 here.</summary>
    [Fact]
    public void NarrowHead_ParenPastTheMargin_TakesTheDot() =>
        Agrees(
            In($"var {N(5)} = {R(96)}.{M(8)}({A(40)});"),
            In($"var {N(5)} = {R(96)}", $"    .{M(8)}({A(40)});")
        );

    /// <summary>Two arguments one column over, the <c>(</c> at 100: the arguments chop, as #528 had them.</summary>
    [Fact]
    public void TwoArgumentsOneOver_ParenAt100_Chops() =>
        Agrees(
            In($"var {N(3)} = {R(74)}.{M(6)}({A(5)}, {A(12)});"),
            In($"var {N(3)} = {R(74)}.{M(6)}(", $"    {A(5)},", $"    {A(12)}", ");")
        );

    /// <summary>And with the <c>(</c> at 101 the dot moves below, where #528 chopped at any <c>(</c>.</summary>
    [Fact]
    public void TwoArgumentsOneOver_ParenAt101_MovesTheDot() =>
        Agrees(
            In($"var {N(3)} = {R(75)}.{M(6)}({A(5)}, {A(11)});"),
            In($"var {N(3)} = {R(75)}", $"    .{M(6)}({A(5)}, {A(11)});")
        );
}
