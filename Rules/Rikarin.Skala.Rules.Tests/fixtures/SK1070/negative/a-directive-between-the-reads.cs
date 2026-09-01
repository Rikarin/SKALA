public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        var key = pair.Item1;
#if DEBUG
        var value = pair.Item2;
#else
        var value = pair.Item2;
#endif
        return key + "=" + value;
    }

    static (string, string) Split(string text) => (text, text);
}
