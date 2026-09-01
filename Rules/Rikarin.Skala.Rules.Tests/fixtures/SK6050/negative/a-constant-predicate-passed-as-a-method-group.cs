using System.Collections.Generic;
using System.Linq;

namespace Contoso.Design;

// ⚠ A predicate whose whole point is that it ignores its input. Nothing in the declaration separates
// this from a stub — only the method group at the call site does, which is why the rule scans for one
// rather than reading the body harder.
public sealed class Filtering {
    public IEnumerable<int> All(IEnumerable<int> items) => items.Where(Always);

    static bool Always(int item) => true;
}
