public static class Overloads {
    static string Describe(string text) => text;

    static string Describe(object value) => "object";

    // `Describe((object)text)` and `Describe(text)` are calls to two different methods.
    public static string Go(string text) => Describe((object)text);
}
