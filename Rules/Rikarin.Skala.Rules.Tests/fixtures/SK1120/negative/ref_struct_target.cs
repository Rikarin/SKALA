// A `ref struct` is a legal `typeof` operand and cannot be the type of a pattern on an `object`.
ref struct Cursor {
    public int Position { get; set; }
}

class RefStructTarget {
    public bool Test(object value) => typeof(Cursor).IsInstanceOfType(value);
}
