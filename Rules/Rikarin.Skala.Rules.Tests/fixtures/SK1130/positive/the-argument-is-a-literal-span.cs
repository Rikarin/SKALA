using System;

// `"world".AsSpan()` unwraps to `"world"`: the pattern takes the string, not a span over it.
public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name) => name.SequenceEqual("world".AsSpan());
}
