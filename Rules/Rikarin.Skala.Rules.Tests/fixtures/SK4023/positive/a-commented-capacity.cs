// ⚠ #302's shape (#325). The guard asked over the argument list's FULL span, which begins at the
// leading trivia of `(` — so a comment on the line above the argument declined the finding. The fix
// replaces `(0)` with `()` and cannot reach anything written before the parenthesis.
using System.Collections.Generic;

static class ListCapacityFixture {
    public static List<int> Make() =>
        new List<int>
            // the default is what we want, spelled out
            (0);
}
