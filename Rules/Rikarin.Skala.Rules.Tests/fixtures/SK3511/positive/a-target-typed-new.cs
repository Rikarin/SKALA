using System;
using System.Threading.Tasks;

public sealed class Session : IAsyncDisposable {
    public int Timeout { get; set; }

    public ValueTask DisposeAsync() => default;
}

public sealed class Consumer {
    static int Configured() => 30;

    public async Task StartAsync() {
        await using Session session = new() { Timeout = Configured() };
        await Task.Yield();
        Console.WriteLine(session.Timeout);
    }
}
