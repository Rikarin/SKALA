using System.Diagnostics.CodeAnalysis;

class Options {
    [SetsRequiredMembers]
    public Options(string path) => Path = path;

    public required string Path { get; init; }
}
