using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contoso.Design;

public sealed class Feed {
    public async IAsyncEnumerable<int> Items() {
        await Task.Yield();

        yield return 1;
    }
}
