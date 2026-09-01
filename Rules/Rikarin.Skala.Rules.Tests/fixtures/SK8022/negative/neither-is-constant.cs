using Xunit;

// No evidence in either direction. Picking one would be a rewrite of somebody's test rather than a
// repair of it.
public sealed class ArchetypeTests {
    [Fact]
    public void Counts() {
        var count = 0;
        var limit = 3;
        Assert.Equal(count, limit);
    }
}
