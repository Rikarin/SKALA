using System.Diagnostics.CodeAnalysis;

// A neighbouring attribute from the same namespace is not this rule's business: SK7051 owns
// `SuppressMessage`, and a rule that reached across would report the same omission twice.
public sealed class Shim {
    [SuppressMessage("Design", "CA1024", Justification = "Reads the filesystem on every call.")]
    public int Read() => 0;

    [return: NotNullIfNotNull(nameof(input))]
    public string? Echo(string? input) => input;
}
