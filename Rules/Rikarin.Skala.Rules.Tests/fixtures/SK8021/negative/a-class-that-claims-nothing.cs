using Microsoft.VisualStudio.TestTools.UnitTesting;

// No class attribute, so there is no declaration of intent for an empty class to contradict. This
// is also every xUnit helper in every repository.
public sealed class Builders {
    public static int Zero() => 0;
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting {
    public sealed class TestClassAttribute : System.Attribute { }

    public class TestMethodAttribute : System.Attribute { }

    public sealed class TestInitializeAttribute : System.Attribute { }
}
