public static class Anonymous {
    // An anonymous type is a reference type: `with` makes a second instance, and `ReferenceEquals`
    // can tell.
    public static bool Distinct() {
        var first = new { Name = "a" };
        var second = first with { };
        return !ReferenceEquals(first, second);
    }
}
