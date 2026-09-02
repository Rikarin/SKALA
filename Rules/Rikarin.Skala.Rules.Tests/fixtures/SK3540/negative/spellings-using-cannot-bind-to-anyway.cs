using System.IO;

// `Dispose(bool)` is the documented pattern's inner half and never the interface member.
public sealed class Layered {
    readonly MemoryStream buffer = new();

    public void Dispose(bool disposing) {
        if (disposing) {
            buffer.Dispose();
        }
    }
}

// Not public, so it was never offered to anybody.
public sealed class Internal {
    readonly MemoryStream buffer = new();

    void Dispose() {
        buffer.Dispose();
    }

    public void Close() {
        Dispose();
    }
}

// Not `void`, so it is not the member `IDisposable` declares.
public sealed class Reporting {
    readonly MemoryStream buffer = new();

    public bool Dispose() {
        buffer.Dispose();
        return true;
    }
}

// Static, so there is no instance for `using` to own.
public sealed class Shared {
    static readonly MemoryStream Buffer = new();

    public static void Dispose() {
        Buffer.Dispose();
    }
}
