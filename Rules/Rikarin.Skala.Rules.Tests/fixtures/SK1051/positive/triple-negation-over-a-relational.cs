// Three `not`s and a relational pattern collapse in one edit. Two overlapping findings — the outer
// pair and then the survivor — would be two fixes for one span.
public sealed class Gate {
    public bool Small(int count) => count is not not not > 5;
}
