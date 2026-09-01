public sealed class Box {
    public int Count;
}

// ⚠ A target-typed conditional has no natural type at all, so the rule cannot ask whether the
// member's type is the conditional's and declines for that reason before it ever reaches the
// value-type check. Reasoning about the conversion the expression was subjected to is exactly what
// this rule does not do.
public sealed class Reader {
    public int? CountOf(Box? box) => box != null ? box.Count : null;
}
