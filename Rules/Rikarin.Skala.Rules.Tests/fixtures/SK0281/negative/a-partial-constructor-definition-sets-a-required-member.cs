using System.Diagnostics.CodeAnalysis;

// #401: the definition is read now, and here the attribute promises something real.
public sealed partial class Options {
    [SetsRequiredMembers]
    public partial Options(int retries);

    public partial Options(int retries) => Retries = retries;

    public required int Retries { get; init; }
}
