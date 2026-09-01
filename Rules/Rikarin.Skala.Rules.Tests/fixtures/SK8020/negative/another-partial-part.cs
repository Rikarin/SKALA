using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public partial class SplitTests { }

// The attribute is on the symbol, not on this declaration; a syntax-only rule would report here.
public partial class SplitTests {
    [TestMethod]
    public void Works() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
