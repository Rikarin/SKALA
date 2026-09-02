using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// "Does this method mutate" is undecidable, so the rule enumerates its evidence instead of inferring
// it. A reading query is not evidence of anything.
public sealed class Tracker {
    readonly List<int> items = [];

    public void Check() {
        Debug.Assert(items.Any());
        Debug.Assert(items.All(item => item > 0));
        Debug.Assert(items.Count(item => item > 0) < 10);
    }
}
