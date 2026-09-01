using System.Collections.Generic;

// ⚠ The defect this rule exists to make unwritable. `a[b.Count - 1]` compiles, is almost always
// wrong, and must never be "corrected" into `a[^1]` — which would be a different program again.
public sealed class Pairs {
    public string Mismatched(List<string> names, List<string> values) => names[values.Count - 1];
}
