public static class Loading {
    static int Read(string path, bool cache = true) => path.Length + (cache ? 1 : 0);

    /// <summary>A documentation comment on the declaration.</summary>
    public static int Load(string path) =>
        // And an ordinary comment on the line above the finding, outside the deleted span.
        Read(path, true);
}
