public sealed record Settings(string Host, int Port, int Retries) {
    public Settings(string Host, int Port) : this(Host, Port, 0) { }
}

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
