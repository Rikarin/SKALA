public sealed class Box {
    public int Count;
}

// The conditional is a target-typed `int?`; `box?.Count` is an `int?` arrived at another way, and
// matching the two would mean reasoning about the conversion rather than about the expression.
public sealed class Reader {
    public int? CountOf(Box? box) => box != null ? box.Count : null;
}
