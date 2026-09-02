using System;
using System.IO;
using System.Threading.Tasks;

// The disposal contract is declared; whether it should also be synchronous is a design
// question and not a missing declaration.
public sealed class Pipeline : IAsyncDisposable {
    readonly MemoryStream buffer = new();

    public void Dispose() {
        buffer.Dispose();
    }

    public ValueTask DisposeAsync() {
        buffer.Dispose();
        return default;
    }
}
