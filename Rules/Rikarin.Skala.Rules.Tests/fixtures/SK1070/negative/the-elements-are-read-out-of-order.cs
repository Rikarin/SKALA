public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        var value = pair.Item2;
        var key = pair.Item1;
        return key + "=" + value;
    }

    static (string, string) Split(string text) => (text, text);
}
