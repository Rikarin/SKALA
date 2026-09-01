public sealed class Document {
    public string? Name;
}

// The receiver is a call, so collapsing two evaluations into one collapses two calls into one.
public sealed class Reader {
    int calls;

    Document? Get() {
        calls++;
        return null;
    }

    public string? NameOf() => Get() != null ? Get()!.Name : null;
}
