// ⚠ SK2101 fires here and is right — `[Pure]` cannot be true of a method that returns nothing — and
// that is recorded in fixture-cross-rule-baseline.txt (#285) rather than repaired. SK2002's guard is
// `ReturnsVoid: false`, so the only shape that can pin it is a `[Pure]` method that returns void,
// which is exactly the shape SK2101 owns. The overlap is unavoidable, not sloppiness: repairing this
// file deletes the guard's only witness.
using System.Diagnostics.Contracts;

class C {
    [Pure]
    static void Validate() { }

    void M() {
        Validate();
    }
}
