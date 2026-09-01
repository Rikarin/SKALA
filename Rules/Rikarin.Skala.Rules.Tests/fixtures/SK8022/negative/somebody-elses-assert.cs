// Not one of the three frameworks' assertion classes, whatever it calls its parameters.
namespace Contoso.Testing {
    public static class Assert {
        public static void Equal<T>(T expected, T actual) { }
    }
}

public sealed class ArchetypeTests {
    public void Counts() {
        var count = 0;
        Contoso.Testing.Assert.Equal(count, 3);
    }
}
