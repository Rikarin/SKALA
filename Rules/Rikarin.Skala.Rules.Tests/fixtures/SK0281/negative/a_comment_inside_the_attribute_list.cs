using System.Diagnostics.CodeAnalysis;

class Options {
    [/* kept for the source generator */ SetsRequiredMembers]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
