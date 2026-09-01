using System;

public sealed class Endpoint {
    public int Port { get; set; }
}

public sealed class Channel : IDisposable {
    public Endpoint Remote { get; } = new();

    public void Dispose() { }
}

public sealed class Consumer {
    // `Remote = { Port = 80 }` assigns through a getter-only property; there is no value to hoist.
    public void Open() {
        using var channel = new Channel { Remote = { Port = 80 } };
        Console.WriteLine(channel.Remote.Port);
    }
}
