// ⚠ The shipped double-report from #307. This is shape 2 exactly — a private, non-readonly field
// the type assigns outside its constructors — and it is *also* `CA2002`'s, because the field is a
// `string`. Re-probed on a pristine net10.0 library at AnalysisMode=All: `CA2002` fires on a
// mutable `string` field, on a readonly one, and on a `string` local, and is silent on a mutable
// `object` field, which is the shape `a-field-reset-in-a-method.cs` keeps as a positive.
//
// `a-string-field-locked.cs` is the neighbouring case where the field is written only by its
// initializer, so shape 2 declines for its own reason and the overlap stayed theoretical. Here it
// does not: without the weak-identity exclusion this file draws SK3061 and CA2002 on one line.
//
// ADR-008 is host, never rebuild.
public sealed class Interner {
    string key = "gate";

    int hits;

    public void Touch() {
        lock (key) {
            hits++;
        }
    }

    public void Rotate() => key = "other";
}
