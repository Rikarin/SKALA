using System;
using System.IO;
using System.Threading.Tasks;

sealed class Resource : IAsyncDisposable {
    public ValueTask DisposeAsync() => default;
}

sealed class Store {
    readonly MemoryStream stream = new();
    readonly Resource resource = new();
}
