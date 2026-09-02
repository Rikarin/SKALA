// ⚠ #302's shape (#325). The guard asked over the invocation's FULL span, which begins after the
// `=>`, so the sentence explaining the comparison declined the finding. The fix rewrites only
// `name.SequenceEqual("world")` into a constant pattern and leaves the comment where it is.
using System;

public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name) =>
        // compared against the graph's own spelling of the name
        name.SequenceEqual("world");
}
