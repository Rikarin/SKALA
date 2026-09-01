// A tab has no single-line raw spelling that a reader could see, and `\t` is not an escape this
// rule simplifies.
public sealed class Columns {
    public string Row() => "a\\b\tc";
}
