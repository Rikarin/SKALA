using System;
using Xunit;

// A test that reads the real clock has made that choice deliberately; `SK8007` is the rule with an
// opinion about it, and this one excludes test code outright so the two cannot both fire.
public sealed class ClockTests {
    [Fact]
    public void MovesForward() {
        var first = DateTime.UtcNow;
        Assert.True(first <= DateTime.UtcNow);
    }
}
