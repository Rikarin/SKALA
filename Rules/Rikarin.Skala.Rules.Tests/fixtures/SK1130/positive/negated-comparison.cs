using System;

public static class Transforms {
    public static bool IsNotWorld(ReadOnlySpan<char> name) => !name.SequenceEqual("world");
}
