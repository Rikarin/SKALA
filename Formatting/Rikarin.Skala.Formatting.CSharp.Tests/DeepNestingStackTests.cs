namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ The document builder and the layout recurse once per level of syntax, and a stack overflow kills
///     the process: no SK9099, no reproduction, no other file formatted. Skala's own
///     <c>Caching/DiagnosticCache.cs</c>, 52 levels deep, overflowed a 1 MB stack under Debug once two
///     formatter rounds each grew a frame. Every format now runs on its own large stack
///     (<see cref="CSharpFormatter" />), and these cells are far deeper than a 1 MB stack holds.
/// </summary>
public sealed class DeepNestingStackTests {
    [Theory]
    [InlineData(600)]
    [InlineData(1_500)]
    public void ADeeplyParenthesisedExpression_FormatsWithoutOverflowing(int depth) {
        var value = new string('(', depth) + "a + b" + new string(')', depth);
        var result = Format.Run($"class C {{\n    int M(int a, int b) => {value};\n}}\n");

        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
    }

    [Fact]
    public void ADeeplyNestedCallChain_FormatsWithoutOverflowing() {
        var value = string.Concat(Enumerable.Repeat("F(", 800)) + "0" + new string(')', 800);
        var result = Format.Run($"class C {{\n    static int F(int x) => x;\n    int M() => {value};\n}}\n");

        Assert.NotEqual(FormatOutcome.NotParseable, result.Outcome);
    }
}
