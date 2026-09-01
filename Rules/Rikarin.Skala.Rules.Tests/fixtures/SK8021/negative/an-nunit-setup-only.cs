using NUnit.Framework;

[TestFixture]
public sealed class Hooks {
    [SetUp]
    public void Before() { }
}

namespace NUnit.Framework {
    public sealed class TestFixtureAttribute : System.Attribute { }

    public class TestAttribute : System.Attribute { }

    public sealed class SetUpAttribute : System.Attribute { }
}
