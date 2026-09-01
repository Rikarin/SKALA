using System;
using System.IO;
using System.Threading.Tasks;

public class Writer : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public ValueTask DisposeAsync() => DisposeAsyncCore();

    protected virtual async ValueTask DisposeAsyncCore() {
        await stream.DisposeAsync();
    }
}

public sealed class Passthrough : Writer {
    protected override ValueTask DisposeAsyncCore() => base.DisposeAsyncCore();
}
