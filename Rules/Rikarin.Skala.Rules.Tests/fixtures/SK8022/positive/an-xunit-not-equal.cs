using Xunit;

public sealed class ArchetypeTests {
    [Fact]
    public void Differs() {
        var name = "a";
        Assert.NotEqual(name, "b");
    }
}
