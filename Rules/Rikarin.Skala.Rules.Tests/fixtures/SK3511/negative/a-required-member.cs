using System;

public sealed class Channel : IDisposable {
    public required string Name { get; set; }

    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ The object initializer is what satisfies the requirement, so `new Channel()` alone is
    // CS9035 and the hoist is illegal rather than merely awkward.
    public void Open() {
        using var channel = new Channel { Name = "main" };
        Console.WriteLine(channel.Name);
    }
}
