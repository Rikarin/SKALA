using System;

// ⚠ This one *does* reach `MemoryExtensions.SequenceEqual`, through the implicit span conversion, and
// it still has no rewrite: `chars is "world"` is CS0029, because a pattern has no conversion step.
// Same guard as the string case, a different reason — which is why both are here.
public static class Names {
    public static bool IsWorld(char[] chars) => chars.SequenceEqual("world");
}
