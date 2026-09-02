using System.Collections.Generic;
using System.Diagnostics;

// ⚠ An `out var` was built as a fifth kind of evidence for this rule and then removed, because the
// compiler already owns the case. Where the variable is *read below the assertion*, deleting the call
// leaves it unassigned and the read is CS0165 — so the positive fixture written for it could not be
// made to compile, which is how the claim was refuted rather than argued. Where it is *not* read, as
// here, nothing is wrong at all: the value was never wanted.
public sealed class Tracker {
    readonly Dictionary<int, string> names = [];

    public void Check(int id) {
        Debug.Assert(names.TryGetValue(id, out var found));
        Debug.Assert(names.TryGetValue(id, out _));
        Debug.Assert(names.ContainsKey(id));
    }
}
