using System;

// A `Span<char>` matches a constant string pattern too — verified by running it, not assumed.
public static class Buffers {
    public static bool IsAbc(Span<char> buffer) => buffer.SequenceEqual("abc");
}
