using Microsoft.VisualStudio.TestTools.UnitTesting;

// Abstract is the escape hatch and the repair for the one case this rule does not decide: a base
// that exists only for its derived fixtures to extend.
[TestClass]
public abstract class FixtureBase {
    protected static int Build() => 0;
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class TestInitializeAttribute : System.Attribute { }
}
