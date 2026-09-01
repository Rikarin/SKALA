// The hook exists so derived types can fill it in. Nothing is lost by not calling it, and reporting
// here would put a `base.DisposeAsyncCore()` into every leaf of every such hierarchy.

using System;
using System.Threading.Tasks;

public class Empty : IAsyncDisposable {
    public ValueTask DisposeAsync() => DisposeAsyncCore();

    protected virtual ValueTask DisposeAsyncCore() => default;
}

public sealed class Extra : Empty {
    protected override async ValueTask DisposeAsyncCore() {
        await Task.Yield();
    }
}
