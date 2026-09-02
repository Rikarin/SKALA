using System.IO;

public abstract class Store { }

public abstract class Cache { }

// The fix inserts at a position rather than replacing a node, and under `#if` the position it
// names is not the position every branch compiles at. The finding is withheld.
public sealed class Index :
#if NET
    Store
#else
    Cache
#endif
{
    readonly MemoryStream pages = new();

    public void Dispose() {
        pages.Dispose();
    }
}
