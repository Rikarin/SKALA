using NUnit.Framework;

// NUnit's classic form takes `object, object`, so the two parameter types are equal and the swap is
// as safe here as it is under xUnit's generic overload.
public sealed class ArchetypeTests {
    public void Counts() {
        object count = 0;
        Assert.AreEqual(count, 3);
    }
}

namespace NUnit.Framework {
    public static class Assert {
        public static void AreEqual(object expected, object actual) { }

        public static void That(object actual, object constraint) { }
    }
}
