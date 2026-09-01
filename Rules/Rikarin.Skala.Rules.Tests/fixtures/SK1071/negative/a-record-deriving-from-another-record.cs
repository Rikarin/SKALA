public record Base {
    public int Weight { get; init; }
}

public sealed record Settings(string Host, int Port) : Base;

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
