using Xunit;

public sealed class ArchetypeTests {
    [Fact]
    public void Substring() {
        // Two parameters on xUnit's own `Assert`, and neither is called what this rule matches, so
        // the shape is left alone.
        var text = "abc";
        Assert.Contains("b", text);
    }
}
