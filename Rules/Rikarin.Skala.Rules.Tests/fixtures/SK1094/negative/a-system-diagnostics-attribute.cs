// System.Diagnostics.CodeAnalysis is deliberately untouched.
using System.Diagnostics.CodeAnalysis;

public sealed class Store {
    [MaybeNull]
    public string Name { get; set; } = "x";
}
