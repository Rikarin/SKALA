using System.Diagnostics.CodeAnalysis;

// #401: attributes of a partial constructor are written on its definition, which Roslyn's driver
// never visits; the attribute was unreported until the analyzers dispatched the definition.
public sealed partial class Options {
    [SetsRequiredMembers]
    public partial Options(int retries);

    public partial Options(int retries) => Retries = retries;

    public int Retries { get; }
}
