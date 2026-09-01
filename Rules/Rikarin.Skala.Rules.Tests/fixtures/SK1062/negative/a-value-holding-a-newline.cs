// A single-line raw string cannot hold a newline, and the multi-line form is an indentation
// decision rather than a mechanical rewrite.
public sealed class Lines {
    public string Pair() => "a\\b\nc";
}
