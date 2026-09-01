// A single `not` over an or-pattern has no shorter spelling, and unwrapping the parentheses would
// change what `and` binds to.
public sealed class Gate {
    public bool Neither(int value) => value is not (1 or 2);
}
