// ⚠ `(uint)hash` throws for a negative hash here, and `(int)` throws again on the way back.
// `>>>` never throws, so the two are not the same program.
public sealed class Guarded {
    public int High(int hash) {
        checked {
            return (int)((uint)hash >> 16);
        }
    }
}
