using System.Runtime.CompilerServices;

namespace Contoso.Design;

// ⚠ Recognised by having a parameterless `GetAwaiter`, not by being on a list. Without that, a method
// returning somebody's own awaitable and correctly named `…Async` would be reported for a suffix it
// has earned — the exact opposite of what the rule is for.
public readonly struct Deferred {
    public DeferredAwaiter GetAwaiter() => default;
}

public readonly struct DeferredAwaiter : INotifyCompletion {
    public bool IsCompleted => true;

    public void OnCompleted(System.Action continuation) => continuation();

    public void GetResult() {
    }
}

public sealed class Store {
    public Deferred SaveAsync() => default;
}
