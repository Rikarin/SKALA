public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) {
        settings = settings with { Host = settings.Host.Trim() };
        return new Settings(settings.Host, port);
    }
}
