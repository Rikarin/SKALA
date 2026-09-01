using Xunit;

// A `const` is as much a constant as a literal, and no more producible by the code under test.
public sealed class ArchetypeTests {
    const int Limit = 3;

    [Fact]
    public void Counts() {
        var count = 0;
        Assert.Equal(count, Limit);
    }
}
