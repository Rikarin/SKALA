using System;

public sealed class Handle : IDisposable {
    public int Value => 42;

    public void Dispose() { }
}

public sealed class Consumer {
    public void Report() {
        var handle = new Handle();
        Console.WriteLine(handle.Value);
    }
}
