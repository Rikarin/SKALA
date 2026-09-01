// `Task` is `IDisposable` and the framework's own guidance is to leave it alone, so the disposal the
// rule would ask for is one nobody should write.

using System;
using System.Threading.Tasks;

public sealed class Pump : IDisposable {
    readonly Task pending = new(static () => { });

    public bool Done => pending.IsCompleted;

    public void Dispose() {
    }
}
