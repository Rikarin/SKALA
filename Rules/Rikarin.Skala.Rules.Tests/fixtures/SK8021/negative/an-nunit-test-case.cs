using NUnit.Framework;

[TestFixture]
public sealed class RowTests {
    [TestCase(1)]
    public void Rows(int value) { }
}

namespace NUnit.Framework {
    public sealed class TestFixtureAttribute : System.Attribute { }

    public class TestAttribute : System.Attribute { }

    public sealed class TestCaseAttribute : System.Attribute {
        public TestCaseAttribute(object value) { }
    }
}
