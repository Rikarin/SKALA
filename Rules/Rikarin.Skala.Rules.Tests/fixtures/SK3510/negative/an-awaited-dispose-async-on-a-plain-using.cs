using System;
using System.Threading.Tasks;

// ⚠ #412's audit: a plain `using` calls `Dispose`, never `DisposeAsync`, so the awaited call is the
// only asynchronous disposal this object gets.
public sealed class Both : IDisposable, IAsyncDisposable {
    public string Log = "";

    public void Dispose() => Log += "sync ";

    public ValueTask DisposeAsync() {
        Log += "async ";
        return default;
    }
}

public static class Probe {
    static async Task<Both> UseAsync() {
        var both = new Both();
        {
            using var resource = both;
            await resource.DisposeAsync();
        }

        return both;
    }

    public static string Run() => UseAsync().GetAwaiter().GetResult().Log;
}
