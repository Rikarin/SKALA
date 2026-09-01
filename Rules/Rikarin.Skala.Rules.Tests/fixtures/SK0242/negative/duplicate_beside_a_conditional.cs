#nullable enable

#nullable enable

#if DEBUG
sealed class Debugging { }
#endif

sealed class Reader {
    public string? Path { get; init; }
}
