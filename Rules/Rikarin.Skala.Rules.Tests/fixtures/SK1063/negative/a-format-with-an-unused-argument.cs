// A gap in the indices leaves an argument the interpolated form has nowhere to put.
public sealed class Partial {
    public string Line(string first, string second) => string.Format("{0}", first, second);
}
