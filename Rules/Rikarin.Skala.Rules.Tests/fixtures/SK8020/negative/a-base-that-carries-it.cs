using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class FixtureBase { }

// `[TestClass]` is inherited, so MSTest opens this type and the attribute is not missing.
public sealed class DerivedTests : FixtureBase {
    [TestMethod]
    public void Works() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
