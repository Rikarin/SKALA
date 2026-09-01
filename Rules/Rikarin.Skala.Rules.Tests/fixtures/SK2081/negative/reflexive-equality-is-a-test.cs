using System.Collections.Generic;

public sealed class EqualityContract {
    // `a.Equals(a)` is outside the table on purpose: this is what an equality test asserts.
    public static bool IsReflexive(HashSet<string> set) => set.Equals(set);
}
