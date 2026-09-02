// ⚠ #302's shape (#325). The guard asked over the outer cast's FULL span, which begins after the
// `=>`, so the one sentence a reader needs — why the shift is unsigned — declined the finding. The
// fix rewrites only `(int)((uint)hash >> 16)` into `hash >>> 16`.
public sealed class Hashing {
    public int High(int hash) =>
        // zero-extend rather than sign-extend, which is the whole point of the round trip
        (int)((uint)hash >> 16);
}
