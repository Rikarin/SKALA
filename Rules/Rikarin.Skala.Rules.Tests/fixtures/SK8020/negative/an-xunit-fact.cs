using Xunit;

// xUnit discovers a `[Fact]` in any public class and has no class attribute to be missing. Firing
// here would report a convention rather than a defect.
public sealed class ArchetypeTests {
    [Fact]
    public void Counts() {
        Assert.Equal(1, 1);
    }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
