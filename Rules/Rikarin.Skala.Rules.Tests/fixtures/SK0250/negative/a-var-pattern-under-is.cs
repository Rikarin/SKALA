public static class Matching {
    // `value is var _` cannot become `value is var`, and a bare `_` under an `is` is CS0246 —
    // the parser reads it as the name of a type.
    public static bool Always(object value) => value is var _;
}
