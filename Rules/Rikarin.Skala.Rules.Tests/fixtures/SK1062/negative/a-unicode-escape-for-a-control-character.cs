// `\u000a` is a newline. Writing it as the character it denotes would put a line break
// inside a literal that cannot hold one.
public sealed class Control {
    public string Break() => "\u000a";
}
