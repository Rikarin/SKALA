using System;

public sealed class Channel : IDisposable {
    public string Name { get; set; } = "";

    public void Dispose() { }
}

public sealed class Consumer {
    // No `using` at all: this rule is about the window before the `using` owns the object, and
    // there is no `using` here to have a window before.
    public Channel Build() {
        var channel = new Channel { Name = "main" };
        return channel;
    }
}
