using System;
using Xunit;

// ⚠ The 22-of-38 shape from #303, taken from the reference tree rather than invented: a settle loop
// that polls a wall-clock deadline, inside an xUnit fixture.
//
// `Settle` is test scaffolding and carries no attribute. Its class carries none either, because
// xUnit has no class-level attribute at all — so the attribute walk that excludes `[TestClass]` and
// `[TestFixture]` fixtures saw nothing here and reported both reads. What excludes it now is
// xUnit's own discovery rule: the class holds a `[Fact]`, so the class is a test class.
//
// Reading the real clock is the whole point of a settle loop, and `TimeProvider` is not the repair.
public sealed class TerrainStreamingTests {
    static void Settle(Func<bool> until) {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline && !until()) { }
    }

    [Fact]
    public void Streams() {
        Settle(static () => true);
        Assert.True(true);
    }
}
