using System;
using System.IO;
using System.Threading.Tasks;

public class Root : IAsyncDisposable {
    readonly MemoryStream stream = new();

    public async ValueTask DisposeAsync() => await DisposeAsyncCore();

    protected virtual async ValueTask DisposeAsyncCore() => await stream.DisposeAsync();
}

public sealed class Frozen : Root {
    protected override ValueTask DisposeAsyncCore() => throw new NotSupportedException();
}
