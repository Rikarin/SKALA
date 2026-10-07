using System;

// ⚠ #412's audit: the `using` calls `IDisposable.Dispose`, the explicit implementation. The public
// `Dispose` the code calls is another method, and deleting the call deletes its flush.
public sealed class Handle : IDisposable {
    public string Log = "";

    void IDisposable.Dispose() => Log += "interface ";

    public void Dispose() => Log += "flush ";
}

public static class Probe {
    public static string Run() {
        var handle = new Handle();
        Use(handle);
        return handle.Log;
    }

    static void Use(Handle outer) {
        using var handle = outer;
        handle.Dispose();
    }
}
