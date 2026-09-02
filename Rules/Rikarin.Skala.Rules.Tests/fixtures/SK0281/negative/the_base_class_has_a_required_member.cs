using System.Diagnostics.CodeAnalysis;

class OptionsBase {
    public required string Name { get; init; }
}

class Options : OptionsBase {
    [SetsRequiredMembers]
    public Options(string path) {
        Name = path;
        Path = path;
    }

    public string Path { get; init; }
}
