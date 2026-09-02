using System.Threading.Tasks;

// The signature belongs to the dispatch chain, not to either author.
public abstract class Job {
    public virtual async Task RunAsync() {
        await Task.Delay(1);
    }
}

public sealed class Nightly : Job {
    public override async Task RunAsync() {
        await Task.Delay(5);
    }
}
