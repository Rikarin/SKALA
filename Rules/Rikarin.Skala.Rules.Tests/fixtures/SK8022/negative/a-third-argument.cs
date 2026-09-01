using Xunit;

// Three parameters, so the two-parameter shape does not match and the precision is not disturbed.
public sealed class ArchetypeTests {
    [Fact]
    public void Counts() {
        var value = 0.0;
        Assert.Equal(value, 3.0, 2);
    }
}
