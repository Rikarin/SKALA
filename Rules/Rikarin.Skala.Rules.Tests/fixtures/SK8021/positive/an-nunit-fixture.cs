using NUnit.Framework;

[TestFixture]
public sealed class ArchetypeTests {
    int Build() => 0;
}

namespace NUnit.Framework {
    public sealed class TestFixtureAttribute : System.Attribute { }

    public class TestAttribute : System.Attribute { }

    public sealed class SetUpAttribute : System.Attribute { }
}
