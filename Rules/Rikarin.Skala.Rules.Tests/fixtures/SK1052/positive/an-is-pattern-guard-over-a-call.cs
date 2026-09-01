public sealed class Document {
    public string Describe() => "document";
}

// The pattern spelling of the same guard, over a call on the receiver rather than a field.
public sealed class Reader {
    public string? Describe(Document? document) => document is not null ? document.Describe() : null;
}
