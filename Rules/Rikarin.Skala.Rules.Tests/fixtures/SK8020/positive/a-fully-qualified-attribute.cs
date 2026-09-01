// No `using`, so the fix has to spell `[TestClass]` the way the file spells `[TestMethod]` or the
// text it writes does not compile.
public sealed class QualifiedTests {
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
    public void Works() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
