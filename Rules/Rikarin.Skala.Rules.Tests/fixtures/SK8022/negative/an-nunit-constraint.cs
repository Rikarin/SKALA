using NUnit.Framework;

// `Assert.That` takes the actual value first and is correct that way round. Matching on the
// assertion class alone, or on a list of method names, would report it.
public sealed class ArchetypeTests {
    public void Counts() {
        object count = 0;
        Assert.That(count, Is.EqualTo(3));
    }
}

namespace NUnit.Framework {
    public static class Assert {
        public static void AreEqual(object expected, object actual) { }

        public static void That(object actual, object constraint) { }
    }

    public static class Is {
        public static object EqualTo(object value) => value;
    }
}
