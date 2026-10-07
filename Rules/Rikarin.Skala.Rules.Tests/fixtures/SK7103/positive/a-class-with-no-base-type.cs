// Roslyn resolves this to System.Object's summary -- "Supports all classes in the .NET class
// hierarchy..." -- which documents `object`, not `Ledger`. That is nothing to inherit.
/// <inheritdoc />
public sealed class Ledger {
}
