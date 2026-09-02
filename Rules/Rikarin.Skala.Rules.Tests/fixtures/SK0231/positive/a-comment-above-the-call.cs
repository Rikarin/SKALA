public static class Labels {
    /// <summary>A documentation comment on the declaration, which #302 shows can silence a rule.</summary>
    public static string Name(string text) =>
        // And an ordinary comment on the line above the finding. Neither is inside the span the
        // fix deletes, so neither may suppress it.
        text.ToString();
}
