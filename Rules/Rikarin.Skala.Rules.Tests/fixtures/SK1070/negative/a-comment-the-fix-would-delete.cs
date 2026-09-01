public sealed class Splitter {
    public string Describe(string text) {
        var pair = Split(text);
        var key = pair.Item1;

        // The value half is the interesting one.
        var value = pair.Item2;
        return key + "=" + value;
    }

    static (string, string) Split(string text) => (text, text);
}
