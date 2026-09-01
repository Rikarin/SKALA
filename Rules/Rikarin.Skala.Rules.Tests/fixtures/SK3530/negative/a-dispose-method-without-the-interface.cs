// ⚠ The fixture that makes the interface test load-bearing. This type declares a parameterless
// `Dispose()` and implements neither disposal contract, so `SK3502` owns the shape — and without
// `SK3530`'s `Implements(owner, IDisposable)` guard both rules report on the one field, at the one
// span, with two different remedies.

using System.IO;

public sealed class Handle {
    readonly MemoryStream buffer = new();

    public long Length => buffer.Length;

    public void Dispose() {
    }
}
