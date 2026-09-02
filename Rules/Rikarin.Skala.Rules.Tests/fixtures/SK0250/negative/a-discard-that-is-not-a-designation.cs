public static class Falling {
    // The `_` arm of a switch expression is a *discard pattern*, not a designation on another
    // pattern: there is nothing in front of it for it to be redundant against.
    public static int Rank(object value) =>
        value switch {
            int => 1,
            _ => 0
        };
}
