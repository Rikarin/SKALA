using System;

// A fix that silently deletes something a person wrote is a fix nobody can review.
public static class Transforms {
    public static bool IsWorld(ReadOnlySpan<char> name) =>
        name.SequenceEqual(/* the graph's own name for it */ "world");
}
