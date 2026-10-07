// ⚠ #425: the block's `size` moves out into the enclosing block, where the declaration's `size` — the
// field — becomes that local read before its declaration (CS0844). The runtime sweep found it on
// SK3511's fixture once this rule's fix was marked safe.
public sealed class Res : System.IDisposable {
    public int Size { get; set; }

    public void Dispose() {
    }
}

public sealed class Consumer {
    int size = 3;

    public int Run() {
        using (var resource = new Res { Size = size }) {
            var size = resource.Size + 1;
            return size;
        }
    }
}

public static class Probe {
    public static int Run() => new Consumer().Run();
}
