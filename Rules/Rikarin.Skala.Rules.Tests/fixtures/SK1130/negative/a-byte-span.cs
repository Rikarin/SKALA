using System;

// ⚠ Written to reach an element-type check, and it does not — which is how that check was found to
// be dead and removed. `"GET"u8` is not a compile-time string constant, so the constant check
// declines this one step earlier. Nothing can reach an element-type check here: both parameters of
// `SequenceEqual` are `ReadOnlySpan<T>` of one `T`, and a string constant converts to
// `ReadOnlySpan<char>` and to nothing else, so requiring a constant string already fixes `T`.
public static class Headers {
    public static bool IsGet(ReadOnlySpan<byte> verb) => verb.SequenceEqual("GET"u8);
}
