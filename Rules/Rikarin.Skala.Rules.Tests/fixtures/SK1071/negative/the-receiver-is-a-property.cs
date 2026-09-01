public sealed record Settings(string Host, int Port);

public sealed class Holder {
    public Settings Current { get; } = new Settings("localhost", 80);

    public Settings WithPort(int port) => new Settings(Current.Host, port);
}
