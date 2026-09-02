public static class BoxedArgument {
    static string Describe(object? value) => value?.ToString() ?? string.Empty;

    // The parameter is `object?`, not `int?`, so nothing at this position writes the nullable type
    // down — and what reaches the method is a box either way, which is a question this rule declines
    // to answer rather than answers.
    public static string Go(int value) => Describe(new int?(value));
}
