public sealed class Interner {
    string key = "gate";

    int hits;

    public void Touch() {
        // ⚠ `CA2002`'s third shape, measured firing on both a literal and a `string` field once its
        // severity is raised. Skala does not restate it.
        //
        // ⚠ The boundary is not as clean as `lock (this)`, and it is recorded rather than smoothed
        // over: this rule's shape 2 is gated on the field's *mutability*, not on its type, so a
        // private `string` gate that the type reassigned outside a constructor would carry both
        // `SK3061` and `CA2002`. Here the field is written only by its initializer, so `SK3061` is
        // silent for its own reason and the overlap stays theoretical.
        lock (key) {
            hits++;
        }
    }
}
