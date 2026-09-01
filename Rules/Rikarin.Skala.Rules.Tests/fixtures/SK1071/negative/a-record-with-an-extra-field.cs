public sealed record Settings(string Host, int Port) {
    readonly int created = 1;

    public int Created => created;
}

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
