using System;
using System.Threading.Tasks;

// The task carries the exception to whoever awaits it, which is the whole difference.
public sealed class Panel {
    public async Task RefreshAsync() {
        await Task.Yield();
        throw new InvalidOperationException("nothing to refresh");
    }
}
