using System.Diagnostics.CodeAnalysis;

class OptionsBase {
    public string Name { get; init; }
}

class Options : OptionsBase {
    [SetsRequiredMembers]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
