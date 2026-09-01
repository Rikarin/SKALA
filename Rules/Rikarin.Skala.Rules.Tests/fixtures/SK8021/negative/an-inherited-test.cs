using Microsoft.VisualStudio.TestTools.UnitTesting;

// The shared-fixture shape: the base holds the cases and the derived class parameterises them.
// Looking only at the type's own members would report here.
public abstract class ContractTests {
    [TestMethod]
    public void Contract() { }
}

[TestClass]
public sealed class ArchetypeContractTests : ContractTests { }

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class TestInitializeAttribute : System.Attribute { }
}
