using System;
using System.Diagnostics.CodeAnalysis;

class Options {
    [SetsRequiredMembers, Obsolete("use the factory")]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
