public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        string key = pair.Item1, value = pair.Item2;
        return key + "=" + value;
    }

    static (string, string) Split(string text) => (text, text);
}
