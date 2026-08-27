using System.IO;
using System.Threading.Tasks;

public sealed class Closer {
    // `Dispose()` returns `void` and `DisposeAsync()` a `ValueTask`, so the rewrite is only
    // available where the call is a whole statement — which is where it always is.
    public async Task CloseAsync(Stream target) {
        await target.FlushAsync();
        target.Dispose();
    }
}
