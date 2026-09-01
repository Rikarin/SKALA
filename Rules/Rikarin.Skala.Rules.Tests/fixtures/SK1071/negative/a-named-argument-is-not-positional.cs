public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) =>
        new Settings(Host: settings.Host, Port: port);
}
