using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The comparison's exemption for the spread gap Skala governs on purpose (#513, SK-DIV-0310) must
///     take out that gap and nothing else, or it would hide real divergences behind a decision.
/// </summary>
public sealed class SkalaGovernedGapsTests {
    [Theory]
    [InlineData("int[] x = [0, .. a, 4];", "int[] x = [0, ..a, 4];")]
    [InlineData("int[] x = [0, ..   a, 4];", "int[] x = [0, ..a, 4];")]
    [InlineData("int[] x = [.. a, .. b];", "int[] x = [..a, ..b];")]
    public void TheSpreadGap_IsTakenOut(string text, string expected) =>
        Assert.Equal(expected, SkalaGovernedGaps.Normalise(text));

    [Theory]
    [InlineData("var r = a[1 .. 3];")]
    [InlineData("var r = a[.. ^1];")]
    [InlineData("var b = a is [1, .. var rest];")]
    [InlineData("var e = new[] { .. a };")]
    [InlineData("int[] x = [0, ..\n    a];")]
    [InlineData("int[] x = [0, .. /* c */ a];")]
    [InlineData("""var s = "[0, .. a]";""")]
    public void EveryOtherGap_IsLeftAlone(string text) => Assert.Equal(text, SkalaGovernedGaps.Normalise(text));

    /// <summary>
    ///     ⚠ The sabotage half: two texts that differ anywhere but the spread gap still differ, and the
    ///     ratchet still sees them.
    /// </summary>
    [Fact]
    public void Fidelity_StillSeesEveryOtherDifference() {
        var same = Fidelity.Compare([("a.cs", "int[] x = [0, .. a];\n", "int[] x = [0, ..a];\n")]);
        Assert.Equal(1, same.IdenticalFiles);

        var different = Fidelity.Compare([("a.cs", "int[] x = [0, .. a];\n", "int[] x = [0,..a];\n")]);
        Assert.Equal(0, different.IdenticalFiles);
    }
}
