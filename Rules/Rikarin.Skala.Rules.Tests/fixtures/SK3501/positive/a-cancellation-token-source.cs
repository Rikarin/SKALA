using System.Threading;

// Every read hands back a bool or nothing, so nothing the source owns leaves the scope and disposing
// it at the end is exactly right.
public static class Probe {
    public static bool Run() {
        var source = new CancellationTokenSource();
        source.Cancel();
        var cancelled = source.IsCancellationRequested;
        return cancelled;
    }
}
