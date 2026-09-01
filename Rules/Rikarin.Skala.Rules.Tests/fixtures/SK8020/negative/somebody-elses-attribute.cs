using Contoso.Testing;

// A `TestMethodAttribute` that is not MSTest's. Matching on the written name would fire here.
public sealed class ArchetypeTests {
    [TestMethod]
    public void Counts() { }
}

namespace Contoso.Testing {
    public sealed class TestMethodAttribute : System.Attribute { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
