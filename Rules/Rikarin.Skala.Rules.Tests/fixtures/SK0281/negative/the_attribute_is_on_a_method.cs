using System.Diagnostics.CodeAnalysis;

class Options {
    public string Path { get; init; }

    [return: MaybeNull]
    public string Read() => Path;
}
