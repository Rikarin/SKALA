using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fixtures.SK2242;

public static class AsyncIterator {
    // ⚠ Still this rule's subject rather than S4457's: an `async` iterator does not start until
    // something enumerates it, so the deferral is the iterator's and not the `async` machinery's.
    public static async IAsyncEnumerable<string> Lines(string text) {
        ArgumentNullException.ThrowIfNull(text);

        await Task.Yield();
        yield return text;
    }
}
