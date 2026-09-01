using System;
using System.IO;
using System.Threading.Tasks;

public class Link : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public ValueTask DisposeAsync() => stream.DisposeAsync();

    public virtual async ValueTask CloseAsync() {
        await stream.FlushAsync();
    }
}

public sealed class Tunnel : Link {
    public override ValueTask CloseAsync() => default;
}
