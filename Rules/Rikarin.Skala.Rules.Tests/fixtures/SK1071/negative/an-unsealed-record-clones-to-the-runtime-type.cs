public record Settings(string Host, int Port);

public sealed record Extended(string Host, int Port, string Tag) : Settings(Host, Port);

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
