// ⚠ #424: `using IDisposable resource = new Res { Size = 3 };` hoisted is `resource.Size = 3;`, and
// `IDisposable` has no `Size` — CS1061 (#412's audit).
using System;

public sealed class Res : IDisposable {
    public int Size { get; set; }

    public void Dispose() { }
}

public static class Consumer {
    public static void Run() {
        using IDisposable resource = new Res { Size = 3 };
        Console.WriteLine(resource);
    }
}
