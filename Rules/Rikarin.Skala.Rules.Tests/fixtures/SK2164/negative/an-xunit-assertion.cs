using System.Collections.Generic;
using Xunit;

// ⚠ The shape that would otherwise be this rule's worst false positive. None of the three test
// frameworks marks its assertions `[Conditional]`, so the call is never deleted, the effect always
// happens, and there is nothing to report. The rule cannot reach this rather than filtering it out.
public sealed class TrackerTests {
    [Fact]
    public void Finds() {
        var names = new Dictionary<int, string> { [1] = "one" };
        Assert.True(names.TryGetValue(1, out var found));
        Assert.Equal("one", found);
    }

    [Fact]
    public void Removes() {
        var pending = new HashSet<int> { 1 };
        Assert.True(pending.Remove(1));
    }
}
