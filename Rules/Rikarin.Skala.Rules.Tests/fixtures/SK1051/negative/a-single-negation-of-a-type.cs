// One `not` in front of something that is not a relational pattern is the shortest spelling there
// is.
public sealed class Gate {
    public bool NotText(object value) => value is not string;
}
