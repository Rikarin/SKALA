using System;

public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name) => name.SequenceEqual("world");
}
