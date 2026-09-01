public static class Annotated {
    // Both sides are written `string?` and the flow state is MaybeNull on each, so the cast is
    // doing no nullable work.
    public static string? Same(string? maybe) {
        var copy = (string?)maybe;
        return copy;
    }
}
