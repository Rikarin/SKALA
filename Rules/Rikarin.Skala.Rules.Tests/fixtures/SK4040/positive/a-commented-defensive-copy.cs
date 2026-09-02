// ⚠ #302's shape (#325), and the comment that used to silence it is the one a reviewer would most
// want written: the sentence explaining why the copy is deliberate. The guard asked over the getter
// body expression's FULL span, which starts after the `=>` — so the justification declined the
// finding, while the fix rewrites only `entries.ToList()` and leaves the sentence alone.
using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<string> entries = new();

    public IReadOnlyList<string> Items =>
        // a deliberate defensive copy — callers have been known to mutate what they get
        entries.ToList();
}
