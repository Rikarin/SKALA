// ⚠ #424: the value `size` is the field where it is written and the block's own `size` where it is
// hoisted to — CS0844, a local used before its declaration (#412's audit).
using System;

public sealed class Res : IDisposable {
    public int Size { get; set; }

    public void Dispose() { }
}

public sealed class Consumer {
    int size = 3;

    public void Run() {
        using (var resource = new Res { Size = size }) {
            var size = resource.Size + 1;
            Console.WriteLine(size);
        }
    }
}
