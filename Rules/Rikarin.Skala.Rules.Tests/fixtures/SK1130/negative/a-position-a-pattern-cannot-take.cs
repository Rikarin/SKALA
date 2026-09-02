using System;

// `name is "world" == flag` hands `"world" == flag` to a grammar that parses constant patterns. The
// rule does not invent parentheses the author did not write.
public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name, bool flag) => name.SequenceEqual("world") == flag;
}
