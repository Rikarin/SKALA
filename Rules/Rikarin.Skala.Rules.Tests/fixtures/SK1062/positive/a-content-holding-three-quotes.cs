// The only shape in this set that actually needs a fence longer than three: the content holds a
// run of exactly three quotes, so the delimiter has to be four.
public sealed class Nested {
    public string Fence() => "a\"\"\"b";
}
