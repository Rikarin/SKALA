public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) =>
        new Settings(
            settings.Host,
            // The caller has already clamped this.
            port
        );
}
