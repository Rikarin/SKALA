// The declaration cannot express a narrowing: `string s = o;` does not compile.
public sealed class Narrow {
    public string Get(object o) {
        var text = (string)o;
        return text;
    }
}
