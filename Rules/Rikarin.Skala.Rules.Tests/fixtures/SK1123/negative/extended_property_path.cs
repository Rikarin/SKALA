class Inner {
    public int Status { get; set; }
}

class Outer {
    public Inner Nested { get; set; } = new();
}

// An extended path carries more than a name, and a name comparison would not see a difference.
class ExtendedPath {
    public bool Editable(Outer o) => o is { Nested.Status: 1 } or { Nested.Status: 2 };
}
