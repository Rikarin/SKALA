// The pattern's input is the property's type, not the `is` operand's, and this rule does not walk
// through a subpattern to find out which member is being matched.
public sealed class Box {
    public int Count { get; init; }
}

public sealed class Gate {
    public bool Small(Box box) => box is { Count: not (> 5) };
}
