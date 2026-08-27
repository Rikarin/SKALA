using System.Threading;

public sealed class Deadline {
    // The source outlives nothing: `Work` is synchronous and has returned before the end of the
    // scope, so disposing there is exactly right.
    public void Run() {
        var source = new CancellationTokenSource();
        source.CancelAfter(1000);
        Work(source.Token);
    }

    static void Work(CancellationToken token) { }
}
