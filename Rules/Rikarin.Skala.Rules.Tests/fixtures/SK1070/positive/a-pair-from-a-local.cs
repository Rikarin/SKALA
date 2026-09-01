public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        var key = pair.Item1;
        var value = pair.Item2;
        return key + "=" + value;
    }

    static (string, string) Split(string text) => (text, text);
}
