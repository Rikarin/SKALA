using System.Runtime.CompilerServices;

public static class Checks {
    public static void Require(
        bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null,
        string? because = null
    ) { }
}
