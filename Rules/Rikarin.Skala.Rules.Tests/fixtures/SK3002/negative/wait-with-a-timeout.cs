using System;
using System.Threading.Tasks;

public sealed class Runner {
    public async Task<bool> RunAsync(Task work) {
        // A bounded wait can return false. `await` has no equivalent, so this is a different
        // program rather than a slower spelling of the same one.
        var finished = work.Wait(TimeSpan.FromSeconds(1));
        await Task.Yield();
        return finished;
    }
}
