public static class NamedArgument {
    static bool Accept(int? value) => value.HasValue;

    // A named argument is refused: the shape guard asks for a bare single argument, and the fix's
    // two spans are described against that.
    public static bool Go(int value) => Accept(value: new int?(value));
}
