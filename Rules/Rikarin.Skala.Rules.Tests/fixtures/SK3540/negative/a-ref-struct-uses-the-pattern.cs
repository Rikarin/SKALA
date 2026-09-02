using System.IO;

// SK3532's subject, not this one. A `ref struct` binds to `using` through the language's
// pattern rule with no interface at all, so this `Dispose()` *is* the declared contract.
public ref struct Window {
    readonly MemoryStream buffer;

    public Window(MemoryStream target) {
        buffer = target;
    }

    public void Dispose() {
        buffer.Dispose();
    }
}
