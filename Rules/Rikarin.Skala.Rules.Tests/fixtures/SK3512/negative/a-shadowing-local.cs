using System;

public sealed class Handle : IDisposable {
    public void Dispose() { }
}

public sealed class Factory {
    // A local of the same name in a different method is a different symbol; ownership is read from
    // the declarator rather than from the identifier.
    public void Scoped() {
        using var handle = new Handle();
        Console.WriteLine(handle);
    }

    public Handle Take() {
        var handle = new Handle();
        return handle;
    }
}
