using System;

namespace Contoso.Design;

// `this is IDisposable` asks what this instance can do, not where it sits in a hierarchy — nothing
// about it inverts a dependency. ⚠ It declines through the base-type walk rather than through an
// interface check: `DerivesFrom` visits only classes, so an interface is never found above `Handle`.
// A separate `TypeKind` guard was written first and no sabotage could turn it red.
public class Handle {
    public void Release() {
        if (this is IDisposable disposable) {
            disposable.Dispose();
        }
    }
}
