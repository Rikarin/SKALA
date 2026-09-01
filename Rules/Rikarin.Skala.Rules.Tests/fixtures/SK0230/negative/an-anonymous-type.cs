public static class Marker {
    // An anonymous type is a different node: there is no `new ()` for `new { }` to become.
    public static object Empty() => new { };
}
