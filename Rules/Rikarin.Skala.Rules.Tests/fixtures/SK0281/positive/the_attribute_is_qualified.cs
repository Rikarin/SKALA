class Options {
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
