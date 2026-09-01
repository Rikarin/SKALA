using System;
using System.IO;
using System.Threading.Tasks;

public class Channel : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public virtual async ValueTask DisposeAsync() {
        await stream.DisposeAsync();
    }
}

public sealed class Framed : Channel {
    readonly MemoryStream frames = new();

    public override async ValueTask DisposeAsync() {
        await frames.FlushAsync();
    }
}
