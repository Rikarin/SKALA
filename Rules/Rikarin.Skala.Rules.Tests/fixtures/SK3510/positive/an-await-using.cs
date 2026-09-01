using System;
using System.Threading.Tasks;

public sealed class Channel : IAsyncDisposable {
    public ValueTask DisposeAsync() => default;

    public ValueTask SendAsync() => default;
}

public sealed class Sender {
    public async Task RunAsync() {
        await using var channel = new Channel();
        await channel.SendAsync();
        await channel.DisposeAsync();
    }
}
