using Microsoft.VisualStudio.TestTools.UnitTesting;

// The shared-base pattern MSTest supports: the attribute belongs on the concrete class that
// inherits these methods, and MSTest could not instantiate this one to run them.
public abstract class ContractTests {
    [TestMethod]
    public void Contract() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
