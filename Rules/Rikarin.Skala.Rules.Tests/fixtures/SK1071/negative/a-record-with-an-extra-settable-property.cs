public sealed record Settings(string Host, int Port) {
    public string? Tag { get; init; }
}

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
