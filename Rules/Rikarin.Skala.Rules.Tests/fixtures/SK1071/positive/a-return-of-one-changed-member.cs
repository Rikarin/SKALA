public sealed record Settings(string Host, int Port, int Retries);

public sealed class Builder {
    public Settings WithRetries(Settings settings, int retries) =>
        new Settings(settings.Host, settings.Port, retries);
}
