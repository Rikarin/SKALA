public static class Projections {
    public static object Describe(string text) => new { Length = text.Length };
}
