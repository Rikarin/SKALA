using System.Collections.Generic;

namespace Contoso.Design;

public sealed class Orders {
    public IReadOnlyList<string> Pending(bool closed) {
        if (closed) {
            return null;
        }

        return new List<string>();
    }
}
