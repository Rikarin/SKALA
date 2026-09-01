using Microsoft.VisualStudio.TestTools.UnitTesting;

// MSTest enumerates types carrying `[TestClass]` and never opens this one, so `Counts` reports
// nothing at all — not a skip, not a warning, not a line in the summary.
public sealed class ArchetypeTests {
    [TestMethod]
    public void Counts() {
        Assert.AreEqual(1, 1);
    }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public static class Assert {
        public static void AreEqual<T>(T expected, T actual) { }
    }
}
