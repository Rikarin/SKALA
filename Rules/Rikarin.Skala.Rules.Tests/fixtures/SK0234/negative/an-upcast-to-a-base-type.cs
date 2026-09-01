public static class Upcast {
    // `(object)text` and `text` have different types, so this is not an identity conversion — and
    // an upcast is most of what RedundantCast reports in practice.
    public static object Box(string text) => (object)text;
}
