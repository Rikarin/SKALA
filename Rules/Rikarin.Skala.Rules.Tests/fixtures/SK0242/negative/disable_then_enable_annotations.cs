#nullable disable

sealed class Writer {
    public string Path { get; init; }
}

#nullable enable annotations

sealed class Reader {
    public string? Path { get; init; }
}
