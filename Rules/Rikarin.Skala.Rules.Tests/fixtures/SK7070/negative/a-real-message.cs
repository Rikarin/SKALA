using System;

public sealed class Store {
    [Obsolete("Use SaveAsync; this overload blocks the calling thread.")]
    public void Save() { }

    [Obsolete("Removed in 3.0. Call Flush(CancellationToken) instead.", true)]
    public void Flush() { }

    [ObsoleteAttribute(message: "Use Dispose(); Close only exists for the 1.x shape.")]
    public void Close() { }
}
