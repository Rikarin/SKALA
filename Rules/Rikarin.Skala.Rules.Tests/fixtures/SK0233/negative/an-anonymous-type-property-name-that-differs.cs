public static class Projections {
    public static object Describe(string text) => new { Size = text.Length, Label = text };
}
