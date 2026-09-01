using System;

public sealed class Channel : IDisposable {
    public string Name { get; init; } = "";

    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ `init` is assignable in an object initializer and nowhere else, so the hoist would produce
    // text that parses and does not bind. The shape is right and the rule has no fix for it.
    public void Open() {
        using var channel = new Channel { Name = "main" };
        Console.WriteLine(channel.Name);
    }
}
