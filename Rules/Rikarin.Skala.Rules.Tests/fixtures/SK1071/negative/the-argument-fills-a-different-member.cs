public sealed record Settings(string Host, string Fallback);

public sealed class Builder {
    public Settings Swap(Settings settings, string fallback) =>
        new Settings(settings.Fallback, fallback);
}
