sealed class Outer {
    sealed class Inner { }

    Inner? held;

    public object? Held => held;
}
