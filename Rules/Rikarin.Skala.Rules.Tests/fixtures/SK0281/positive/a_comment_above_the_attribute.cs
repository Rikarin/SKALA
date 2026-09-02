using System.Diagnostics.CodeAnalysis;

class Options {
    // the factory is the supported entry point
    [SetsRequiredMembers]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
