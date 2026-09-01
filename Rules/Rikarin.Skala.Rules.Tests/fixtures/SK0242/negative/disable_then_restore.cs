#nullable disable

sealed class Writer {
    public string Path { get; init; }
}

#nullable restore

sealed class Reader {
    public string Path { get; init; } = "";
}
