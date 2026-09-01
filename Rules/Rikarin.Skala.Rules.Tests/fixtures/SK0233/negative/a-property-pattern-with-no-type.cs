public static class Presence {
    // `value is { }` is a null test, and `value is` is not a program.
    public static bool Any(object? value) => value is { };
}
