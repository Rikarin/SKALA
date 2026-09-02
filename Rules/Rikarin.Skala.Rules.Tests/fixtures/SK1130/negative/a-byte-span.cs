using System;

// A constant string pattern is character-only; there is no `is "abc"u8`. The element type is checked
// as well as the type name.
public static class Headers {
    public static bool IsGet(ReadOnlySpan<byte> verb) => verb.SequenceEqual("GET"u8);
}
