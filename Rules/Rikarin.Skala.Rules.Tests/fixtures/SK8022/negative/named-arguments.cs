using Xunit;

// The order is stated explicitly and is correct; swapping the positions would change what it means.
public sealed class ArchetypeTests {
    [Fact]
    public void Counts() {
        var count = 0;
        Assert.Equal(actual: count, expected: 3);
    }
}
