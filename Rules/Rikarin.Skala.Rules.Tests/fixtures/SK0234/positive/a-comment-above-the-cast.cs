public static class Naming {
    /// <summary>A documentation comment on the declaration.</summary>
    public static string Same(string text) =>
        // And an ordinary comment on the line above the finding, outside the deleted span.
        (string)text;
}
