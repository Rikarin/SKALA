using System;
using System.IO;
using System.Threading.Tasks;

public class Writer : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public async ValueTask DisposeAsync() {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore() {
        await stream.DisposeAsync();
    }
}

public sealed class Buffered : Writer {
    readonly MemoryStream queue = new();

    protected override async ValueTask DisposeAsyncCore() {
        await queue.FlushAsync();
    }
}
