// `var x = […]` is CS9176: a collection expression has no natural type, so there is nothing for
// `var` to infer. This is the single most likely way to get this rule wrong.
public sealed class Names {
    public string[] All() {
        var names = new[] { "a", "b" };
        return names;
    }
}
