using System.Collections.Concurrent;

namespace Contoso.Design;

// A shared cache exists to be written through by many callers at once, so a non-reassignable field
// is the whole of what `readonly` was ever meant to say here.
public sealed class Cache {
    public readonly ConcurrentDictionary<string, int> Entries = new();

    public readonly ConcurrentQueue<string> Pending = new();
}
