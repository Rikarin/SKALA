using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public partial class SplitTests { }

public partial class SplitTests {
    [TestMethod]
    public void Works() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class TestInitializeAttribute : System.Attribute { }
}
