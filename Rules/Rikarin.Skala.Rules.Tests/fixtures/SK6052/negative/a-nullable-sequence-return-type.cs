using System.Collections.Generic;

namespace Contoso.Design;

// ⚠ The intended way to disagree with this rule. `IReadOnlyList<string>?` is the author saying null is
// a value this method returns; the contract already carries the warning that an unguarded `foreach`
// would be wrong, and the rule has nothing to add to a decision made on purpose.
public sealed class Orders {
    public IReadOnlyList<string>? Pending(bool closed) {
        if (closed) {
            return null;
        }

        return new List<string>();
    }
}
