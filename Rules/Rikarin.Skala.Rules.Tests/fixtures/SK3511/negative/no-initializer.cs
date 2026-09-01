using System;

public sealed class Channel : IDisposable {
    public string Name { get; set; } = "";

    public void Dispose() { }
}

public sealed class Consumer {
    // The repaired shape: constructed, then assigned inside the scope the `using` protects.
    public void Open() {
        using var channel = new Channel();
        channel.Name = "main";
        Console.WriteLine(channel.Name);
    }
}
