using Microsoft.VisualStudio.TestTools.UnitTesting;

// The attribute is a declaration of intent the file no longer keeps: the runner opens the type,
// finds nothing to run, and prints nothing at all.
[TestClass]
public sealed class ArchetypeTests {
    static int Build() => 0;
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class TestInitializeAttribute : System.Attribute { }
}
