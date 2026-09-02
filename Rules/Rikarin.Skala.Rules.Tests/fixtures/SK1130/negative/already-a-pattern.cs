using System;

// The rule's own output. It must not fire on it, or `skala fix` is a loop.
public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name) => name is "world" or "worldViewProjection";
}
