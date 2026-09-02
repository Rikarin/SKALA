using System.Diagnostics.CodeAnalysis;

class Root {
    public required string Name { get; init; }
}

class Middle : Root { }

class Options : Middle {
    [SetsRequiredMembers]
    public Options(string path) {
        Name = path;
        Path = path;
    }

    public string Path { get; init; }
}
