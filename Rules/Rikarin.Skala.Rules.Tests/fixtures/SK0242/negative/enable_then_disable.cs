#nullable enable

sealed class Reader {
    public string? Path { get; init; }
}

#nullable disable

sealed class Writer {
    public string Path { get; init; }
}
