using Microsoft.VisualStudio.TestTools.UnitTesting;

// MSTest requires `[TestClass]` on the type holding `[AssemblyInitialize]`, so this is correct code
// with no test in it and the rule must not report it.
[TestClass]
public sealed class AssemblyHooks {
    [AssemblyInitialize]
    public static void Start() { }
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class AssemblyInitializeAttribute : System.Attribute { }
}
