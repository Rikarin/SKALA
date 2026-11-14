namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ An <c>=</c> before a single method call on a named receiver, <c>r.M(…)</c>, whose line overflows (#446,
///     SK-DIV-0211 round 8): whether the <c>=</c> breaks, the arguments chop beside it, or both. Each pair differs in one
///     width, and every expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class HeldReceiverCallIssue446Tests {
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

    static string Nested(int n) => "x.Y" + new string('y', n - 7) + "(zz)";

    const string Method = "Mmmmmm";

    /// <summary>Two arguments that fit below behind a head of 16: a list of 43 moves below whole.</summary>
    [Fact]
    public void TwoArguments_FitBelow_ListOf43_BreaksTheEquals() =>
        Agrees(
            In($"var {N(10)} = {R(49)}.{Method}({A(10)}, {A(31)});"),
            In($"var {N(10)} =", $"    {R(49)}.{Method}({A(10)}, {A(31)});")
        );

    /// <summary>
    ///     And a list of 44 keeps the <c>=</c> and chops: the plain-name floor at this <c>(</c> is 48, half a column
    ///     lower per column of head short of 24.
    /// </summary>
    [Fact]
    public void TwoArguments_FitBelow_ListOf44_Chops() =>
        Agrees(
            In($"var {N(10)} = {R(49)}.{Method}({A(10)}, {A(32)});"),
            In($"var {N(10)} = {R(49)}.{Method}(", $"    {A(10)},", $"    {A(32)}", ");")
        );

    /// <summary>Two arguments that do not fit below: a list of 43 breaks the <c>=</c> and chops below it.</summary>
    [Fact]
    public void TwoArguments_FitNowhere_ListOf43_BreaksBoth() =>
        Agrees(
            In($"var {N(10)} = {R(61)}.{Method}({A(10)}, {A(31)});"),
            In($"var {N(10)} =", $"    {R(61)}.{Method}(", $"        {A(10)},", $"        {A(31)}", "    );")
        );

    /// <summary>And a list of 44 keeps the <c>=</c>: <c>35 + head / 2</c> is 43.</summary>
    [Fact]
    public void TwoArguments_FitNowhere_ListOf44_Chops() =>
        Agrees(
            In($"var {N(10)} = {R(61)}.{Method}({A(10)}, {A(32)});"),
            In($"var {N(10)} = {R(61)}.{Method}(", $"    {A(10)},", $"    {A(32)}", ");")
        );

    /// <summary>One argument of 70 behind a head of 16 and a callee of 40: the <c>=</c> breaks, the argument chops.</summary>
    [Fact]
    public void OneWideArgument_Of70_BreaksBoth() =>
        Agrees(
            In($"var {N(10)} = {R(33)}.{Method}({A(70)});"),
            In($"var {N(10)} =", $"    {R(33)}.{Method}(", $"        {A(70)}", "    );")
        );

    /// <summary>One of 71 keeps the <c>=</c> and chops beside it, where #528's table broke the <c>=</c> as well.</summary>
    [Fact]
    public void OneWideArgument_Of71_Chops() =>
        Agrees(
            In($"var {N(10)} = {R(33)}.{Method}({A(71)});"),
            In($"var {N(10)} = {R(33)}.{Method}(", $"    {A(71)}", ");")
        );

    /// <summary>An argument that is itself a call is laid out alike: it moves below whole while it fits there.</summary>
    [Fact]
    public void CallArgument_FitsBelow_BreaksTheEquals() =>
        Agrees(
            In($"var {N(18)} = {R(33)}.{Method}({Nested(64)});"),
            In($"var {N(18)} =", $"    {R(33)}.{Method}({Nested(64)});")
        );

    /// <summary>One of 70 that does not fit below breaks the <c>=</c> and chops below it.</summary>
    [Fact]
    public void CallArgument_Of70_BreaksBoth() =>
        Agrees(
            In($"var {N(18)} = {R(33)}.{Method}({Nested(70)});"),
            In($"var {N(18)} =", $"    {R(33)}.{Method}(", $"        {Nested(70)}", "    );")
        );

    /// <summary>And one of 72 keeps the <c>=</c> and chops beside it.</summary>
    [Fact]
    public void CallArgument_Of72_Chops() =>
        Agrees(
            In($"var {N(18)} = {R(33)}.{Method}({Nested(72)});"),
            In($"var {N(18)} = {R(33)}.{Method}(", $"    {Nested(72)}", ");")
        );

    static string T(int n) => new('T', n);

    /// <summary>A typed local of type 20 and name 24: a list of 53 moves below whole.</summary>
    [Fact]
    public void Typed_ListOf53_BreaksTheEquals() =>
        Agrees(
            In($"{T(20)} {N(24)} = {R(33)}.{Method}({A(10)}, {A(41)});"),
            In($"{T(20)} {N(24)} =", $"    {R(33)}.{Method}({A(10)}, {A(41)});")
        );

    /// <summary>And one of 54 keeps the <c>=</c> and chops.</summary>
    [Fact]
    public void Typed_ListOf54_Chops() =>
        Agrees(
            In($"{T(20)} {N(24)} = {R(33)}.{Method}({A(10)}, {A(42)});"),
            In($"{T(20)} {N(24)} = {R(33)}.{Method}(", $"    {A(10)},", $"    {A(42)}", ");")
        );

    /// <summary>A name of four behind a type of 36 does not reach the call: the arguments chop.</summary>
    [Fact]
    public void Typed_ShortName_Type36_Chops() =>
        Agrees(
            In($"{T(36)} {N(4)} = {R(33)}.{Method}({A(10)}, {A(28)});"),
            In($"{T(36)} {N(4)} = {R(33)}.{Method}(", $"    {A(10)},", $"    {A(28)}", ");")
        );

    /// <summary>Behind a type of 40 it does, and the <c>=</c> breaks.</summary>
    [Fact]
    public void Typed_ShortName_Type40_BreaksTheEquals() =>
        Agrees(
            In($"{T(40)} {N(4)} = {R(33)}.{Method}({A(10)}, {A(28)});"),
            In($"{T(40)} {N(4)} =", $"    {R(33)}.{Method}({A(10)}, {A(28)});")
        );

    /// <summary>One column over, one argument: the type/name gap breaks, as before a bare name.</summary>
    [Fact]
    public void OneOver_OneArgument_BreaksTheTypeNameGap() =>
        Agrees(
            In($"List<{T(24)}> {N(7)} = {R(35)}.M{new string('m', 19)}({A(13)});"),
            In($"List<{T(24)}>", $"    {N(7)} = {R(35)}.M{new string('m', 19)}({A(13)});")
        );

    /// <summary>One column over, two arguments whose <c>(</c> lands at 90: the type/name gap breaks.</summary>
    [Fact]
    public void OneOver_ParenOn90_BreaksTheTypeNameGap() =>
        Agrees(
            In($"List<{T(23)}> {N(15)} = {R(29)}.Mnw({A(17)}, {A(10)});"),
            In($"List<{T(23)}>", $"    {N(15)} = {R(29)}.Mnw({A(17)}, {A(10)});")
        );

    /// <summary>And one whose <c>(</c> lands at 82 chops its arguments instead.</summary>
    [Fact]
    public void OneOver_ParenOn82_Chops() =>
        Agrees(
            In($"List<{T(23)}> {N(3)} = {R(15)}.M{new string('m', 20)}({A(7)}, {A(28)});"),
            In($"List<{T(23)}> {N(3)} = {R(15)}.M{new string('m', 20)}(", $"    {A(7)},", $"    {A(28)}", ");")
        );

    /// <summary>Behind a head of ten, one argument of 21 moves below whole with its call.</summary>
    [Fact]
    public void NarrowHead_ArgumentOf21_BreaksTheEquals() =>
        Agrees(
            In($"var {N(4)} = {R(73)}.{Method}({A(21)});"),
            In($"var {N(4)} =", $"    {R(73)}.{Method}({A(21)});")
        );

    /// <summary>And one of 22 keeps the <c>=</c> and chops, where #528's <c>below ≤ width + 1</c> broke it.</summary>
    [Fact]
    public void NarrowHead_ArgumentOf22_Chops() =>
        Agrees(
            In($"var {N(4)} = {R(73)}.{Method}({A(22)});"),
            In($"var {N(4)} = {R(73)}.{Method}(", $"    {A(22)}", ");")
        );
}
