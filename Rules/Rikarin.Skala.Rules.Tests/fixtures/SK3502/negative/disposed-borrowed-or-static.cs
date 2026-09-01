using System;
using System.IO;
using System.Threading.Tasks;

sealed class Store : IDisposable {
    static readonly MemoryStream shared = new();
    readonly MemoryStream borrowed;
    readonly MemoryStream owned = new();

    public Store(MemoryStream borrowed) {
        this.borrowed = borrowed;
    }

    public void Dispose() => owned.Dispose();
}

sealed class AsyncStore : IAsyncDisposable {
    readonly AsyncResource owned = new();

    public ValueTask DisposeAsync() => owned.DisposeAsync();
}

sealed class AsyncResource : IAsyncDisposable {
    public ValueTask DisposeAsync() => default;
}
