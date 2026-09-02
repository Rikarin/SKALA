using System.Collections.Generic;

// The conditional access is inside the assignment rather than the other way round, so the write
// happens on every run and the walk from it stops at the statement without finding one.
public sealed class Tally {
    int total;

    public void Count(List<int>? values) {
        total = values?.Count ?? 0;
    }

    public int Total => total;
}
