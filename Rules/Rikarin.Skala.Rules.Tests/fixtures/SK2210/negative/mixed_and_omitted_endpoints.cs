// ⚠ A mixed range depends on the length: `3..^1` is invalid only when the collection is shorter
// than four, which this rule refuses to guess. An omitted endpoint is the same question — `2..`
// ends at `Length` and `..^1` starts at 0 — so neither is decidable from the constants alone.
class C {
    int[] Mixed(int[] values) => values[3..^1];

    int[] FromThree(int[] values) => values[2..];

    int[] ToLast(int[] values) => values[..^1];
}
