using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The helper carries no attribute of its own, so the containing type is what excludes it. A fixture's
// private helper and its constructor are test code just as much as the test method is.
[TestClass]
public sealed class LedgerTests {
    static DateTime Anchor() => DateTime.UtcNow;

    [TestMethod]
    public void Runs() {
        Assert.IsTrue(Anchor() > DateTime.MinValue);
    }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestClassAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestMethodAttribute : Attribute { }

    public static class Assert {
        public static void IsTrue(bool condition) { }
    }
}
