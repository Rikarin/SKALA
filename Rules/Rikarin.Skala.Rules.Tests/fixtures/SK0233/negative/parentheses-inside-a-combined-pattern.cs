public static class Combined {
    // Inside an `and`/`or` the parentheses can be the precedence.
    public static bool Matches(object value) => value is (int and > 0) or string;
}
