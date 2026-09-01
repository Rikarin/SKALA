public sealed record Settings(string Host, int Port) {
    public string Host { get; init; } = Host.Trim();
}

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
