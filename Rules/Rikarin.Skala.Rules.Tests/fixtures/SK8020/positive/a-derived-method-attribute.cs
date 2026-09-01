using Microsoft.VisualStudio.TestTools.UnitTesting;

// `[DataTestMethod]` derives from `[TestMethod]`, so the base walk reaches it and the written name
// never has to be listed.
public class RowTests {
    [DataTestMethod]
    public void Rows(int value) { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class DataTestMethodAttribute : TestMethodAttribute { }
}
