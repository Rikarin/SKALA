using System;
using System.IO;

public abstract class Owned : IDisposable {
    public abstract void Dispose();
}

// The contract is inherited, so `AllInterfaces` has it and `using` binds. The `new` spelling
// here is a redeclaration of a member the type already offers under the interface.
public sealed class Leaf : Owned {
    readonly MemoryStream buffer = new();

    public override void Dispose() {
        buffer.Dispose();
    }
}
