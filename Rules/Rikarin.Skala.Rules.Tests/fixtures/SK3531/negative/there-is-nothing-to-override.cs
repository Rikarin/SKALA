using System;
using System.IO;
using System.Threading.Tasks;

public sealed class Solo : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public async ValueTask DisposeAsync() {
        await stream.DisposeAsync();
    }
}
