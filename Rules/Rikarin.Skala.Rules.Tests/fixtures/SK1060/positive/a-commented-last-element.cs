// ⚠ #302's shape (#325), and the narrowest over-reach in the batch. The guard asked over the
// bracketed argument list's FULL span — everything from the leading trivia of `[` — while the fix
// rewrites only the offset EXPRESSION inside it. So a comment before the `[` declined the finding,
// and so would one between `[` and the expression, which the fix preserves.
using System.Collections.Generic;

public sealed class Stack {
    readonly List<string> items = new();

    public string Top() =>
        items
            // the last one, counting from the end
            [items.Count - 1];
}
