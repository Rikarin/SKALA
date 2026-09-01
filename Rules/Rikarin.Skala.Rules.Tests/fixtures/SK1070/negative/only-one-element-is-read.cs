public sealed class Splitter {
    public string First(string text) {
        var pair = Split(text);
        var key = pair.Item1;
        return key;
    }

    static (string, string) Split(string text) => (text, text);
}
