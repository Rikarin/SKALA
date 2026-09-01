public record Base(string Name) {
    public int Weight { get; init; }
}

public sealed record Settings(string Name, int Port) : Base(Name);

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Name, port);
}
