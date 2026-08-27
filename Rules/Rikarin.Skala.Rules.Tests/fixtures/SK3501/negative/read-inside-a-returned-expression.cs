using System.Threading;
using System.Threading.Tasks;

public sealed class Deadline {
    // ⚠ The case that decides whether this rule is safe. The returned task is still using the
    // source after this method has returned, so disposing at the end of this scope would be wrong —
    // and the rule cannot tell that apart from `return stream.ReadByte();`, where it would be right.
    // It withholds both.
    public Task RunAsync() {
        var source = new CancellationTokenSource();
        source.CancelAfter(1000);
        return Task.Delay(10, source.Token);
    }
}
