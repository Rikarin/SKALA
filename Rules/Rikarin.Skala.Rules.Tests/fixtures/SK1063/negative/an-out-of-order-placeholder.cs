// The arguments would be evaluated in the order they are printed, not the order they are written.
public sealed class Swapped {
    public string Line(string first, string second) => string.Format("{1} {0}", first, second);
}
