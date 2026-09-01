public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public string Describe(Settings settings, int port) => new Settings(settings.Host, port).Host;
}
