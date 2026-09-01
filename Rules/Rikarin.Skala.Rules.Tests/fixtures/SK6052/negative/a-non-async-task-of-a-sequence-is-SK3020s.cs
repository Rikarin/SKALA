using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contoso.Design;

// ⚠ The disjointness proof, one half. The declared return type is a task, not a sequence, so what is
// returned here is a null *task* — `SK3020`'s finding, and it throws at the caller's `await` before any
// sequence exists. `SK6052` requires the *effective* return type to be a sequence and declines.
// `SK6052`'s positive `an-async-method-returning-a-null-sequence` is the other half: there `SK3020`
// declines by its `async` guard and this rule reports. The two predicates cannot both hold.
public sealed class Feed {
    public Task<IEnumerable<string>> ItemsAsync() {
        return null;
    }
}
