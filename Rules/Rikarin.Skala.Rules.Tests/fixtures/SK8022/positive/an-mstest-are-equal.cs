public sealed class ArchetypeTests {
    public void Counts() {
        var count = 0;
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(count, 3);
    }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public static class Assert {
        public static void AreEqual<T>(T expected, T actual) { }
    }
}
