using System;

public sealed class Inner : IDisposable {
    public void Dispose() { }
}

public sealed class Owner : IDisposable {
    public Inner Child { get; } = new();

    public void Dispose() { }
}

public sealed class Consumer {
    // The `using` owns `owner`, not `owner.Child`, and nothing here disposes the child twice.
    public void Report() {
        using var owner = new Owner();
        owner.Child.Dispose();
    }
}
