// The chain is broken by the shortest override there is, and it reads as "nothing extra to clean up".

using System;
using System.IO;
using System.Threading.Tasks;

public class Pipe : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public async ValueTask DisposeAsync() {
        await DisposeAsyncCore();
    }

    protected virtual async ValueTask DisposeAsyncCore() {
        await stream.DisposeAsync();
    }
}

public sealed class Plain : Pipe {
    protected override ValueTask DisposeAsyncCore() {
        return default;
    }
}
