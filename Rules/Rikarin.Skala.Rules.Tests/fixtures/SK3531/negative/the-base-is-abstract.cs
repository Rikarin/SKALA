using System;
using System.IO;
using System.Threading.Tasks;

public abstract class Source : IAsyncDisposable {
    public async ValueTask DisposeAsync() => await DisposeAsyncCore();

    protected abstract ValueTask DisposeAsyncCore();
}

public sealed class FileSource : Source {
    readonly MemoryStream stream = new();

    protected override async ValueTask DisposeAsyncCore() {
        await stream.DisposeAsync();
    }
}
