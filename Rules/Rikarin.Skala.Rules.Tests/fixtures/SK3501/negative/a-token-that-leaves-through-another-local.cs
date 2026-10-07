using System.Threading;

// ⚠ #412's audit: the token is tied to its source and leaves the method through a second local.
// Disposing the source at the end of the scope makes the caller's `WaitHandle` throw
// ObjectDisposedException.
public static class Probe {
    static CancellationToken Make() {
        var source = new CancellationTokenSource();
        var token = source.Token;
        return token;
    }

    public static bool Run() => Make().WaitHandle is not null;
}
