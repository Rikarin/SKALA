public static class Named {
    const int Start = 0;

    // A named constant that happens to be zero is a name somebody chose.
    public static string From(string text) => text[Start..];
}
