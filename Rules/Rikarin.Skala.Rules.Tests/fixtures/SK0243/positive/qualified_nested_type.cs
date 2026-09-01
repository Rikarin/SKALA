sealed class Outer {
    public sealed class Inner { }

    Outer.Inner? held;

    public object? Held => held;
}
