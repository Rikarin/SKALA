public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        var key = pair.Item1;
        var again = pair.Item1;
        return key + "=" + again;
    }

    static (string, string) Split(string text) => (text, text);
}
