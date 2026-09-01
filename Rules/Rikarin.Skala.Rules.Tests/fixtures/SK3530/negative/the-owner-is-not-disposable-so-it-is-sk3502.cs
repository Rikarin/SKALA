// The other half of the split. `SK3502` owns this shape — the owner declares no disposal contract —
// and the two rules' predicates are each other's negation, so exactly one of them can speak here.

using System.IO;

public sealed class Cache {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;
}
