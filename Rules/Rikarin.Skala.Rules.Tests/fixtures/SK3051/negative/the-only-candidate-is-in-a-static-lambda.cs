using System;
using System.Threading;
using System.Threading.Tasks;

// ⚠ CS8421: a `static` lambda cannot capture the parameter the fix would add, so forwarding the
// token there does not compile — and appending the parameter without forwarding it is the #328
// defect. The only candidate in this body is inside one, so the finding is withdrawn.
public sealed class Scheduler {
    public async Task RunAsync() {
        Func<Task> work = static () => Task.Delay(10);
        await work();
    }
}
