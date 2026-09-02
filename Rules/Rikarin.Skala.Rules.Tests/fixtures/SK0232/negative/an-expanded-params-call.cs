public static class Sums {
    static int Take(int first, params int[] rest) => first + rest.Length;

    // ⚠ Three arguments against two parameters. For an expanded `params` call "the parameter the
    // trailing argument fills" is not a fact at all, and reading `method.Parameters` at the
    // argument's own position is how SK0232 threw `IndexOutOfRangeException` on every such call
    // (#298) — a crash that failed no test, because a crashed analyzer passes every negative.
    public static int Total() => Take(1, 2, 3);

    // The same shape one argument shorter, so the array creation is the argument rather than the
    // element: still more arguments than parameters is not the question, the params role is.
    public static int Pair() => Take(1, 2);
}
