public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings Fresh(string host, int port) => new Settings(host, port);
}
