public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings Copy(Settings settings) => new Settings(settings.Host, settings.Port);
}
