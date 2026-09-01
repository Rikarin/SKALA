// A colon inside a hole is grammar, not text: `{a ? b : c}` would parse `c` as a format specifier.
public sealed class Ternary {
    public string Line(bool flag, string a, string b) => string.Format("{0}", flag ? a : b);
}
