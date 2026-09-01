using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contoso.Design;

// ⚠ The half of the concept `SK3020` cannot reach. `SK3020` excludes `async` methods, because an
// `async` method cannot return a null *task* — the compiler wraps the result. What it can return is a
// null *sequence* inside a perfectly good task, and the caller's `foreach` fails after the `await`.
public sealed class Feed {
    public async Task<IEnumerable<string>> ItemsAsync(bool empty) {
        await Task.Yield();

        return null;
    }
}
