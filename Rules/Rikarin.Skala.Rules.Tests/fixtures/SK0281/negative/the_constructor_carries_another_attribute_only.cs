using System;

class Options {
    [Obsolete("use the factory")]
    public Options(string path) => Path = path;

    public string Path { get; init; }
}
