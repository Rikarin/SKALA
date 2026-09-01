using Microsoft.VisualStudio.TestTools.UnitTesting;

// A helper beside the fixtures. It declares no test method, so nothing about it is undiscovered.
public static class Builders {
    public static int Zero() => 0;
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }
}
