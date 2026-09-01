public static class Dispatch {
    static int Run(string text) => text.Length;

    static int Run(string text, int retries = 0) => text.Length + retries;

    // `Run(text, 0)` and `Run(text)` are calls to two different methods.
    public static int Once(string text) => Run(text, 0);
}
