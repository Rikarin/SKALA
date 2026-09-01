using System;
using System.Collections.Generic;

namespace Contoso.Design;

// ⚠ Methods and local functions only. A property or an indexer returning a null sequence is the same
// defect and is a stated gap — the shape has more forms than a method does and the fixture set for
// them has not been built. A lambda is excluded for a different reason: the `return` inside one
// belongs to the lambda, not to the method around it, so its return type is not the declaration's.
public sealed class Orders {
    public IReadOnlyList<string> Pending => null;

    public Func<IReadOnlyList<string>> Deferred() {
        return () => null;
    }
}
