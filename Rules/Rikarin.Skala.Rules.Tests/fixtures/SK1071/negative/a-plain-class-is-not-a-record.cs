public sealed class Settings {
    public Settings(string Host, int Port) {
        this.Host = Host;
        this.Port = Port;
    }

    public string Host { get; }

    public int Port { get; }
}

public sealed class Builder {
    public Settings WithPort(Settings settings, int port) => new Settings(settings.Host, port);
}
