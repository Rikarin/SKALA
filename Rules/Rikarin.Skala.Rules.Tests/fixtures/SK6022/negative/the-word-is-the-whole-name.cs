public sealed class Class {
    public string Name { get; init; } = string.Empty;
}

public readonly struct Struct {
    public Struct(int fields) {
        Fields = fields;
    }

    public int Fields { get; }
}
