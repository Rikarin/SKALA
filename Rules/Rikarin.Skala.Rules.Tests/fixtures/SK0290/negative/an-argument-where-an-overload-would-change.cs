public static class OverloadedArgument {
    static string Describe(int value) => "int";

    static string Describe(int? value) => "int?";

    // ⚠ `Describe(new int?(5))` and `Describe(5)` are calls to two different methods. The parameter's
    // type says `int?` at this position and it is still not enough.
    public static string Go() => Describe(new int?(5));
}
