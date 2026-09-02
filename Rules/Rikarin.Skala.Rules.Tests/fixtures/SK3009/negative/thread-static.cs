// ⚠ This file contains a real defect and SK2100 reports it here, which is correct and recorded in
// fixture-cross-rule-baseline.txt (#285): a `[ThreadStatic]` field with an initializer is initialized
// on the first thread to touch the type and every other thread sees null. It stays, because SK3009
// reads the field INITIALIZER — a `[ThreadStatic] Lazy<T>` without one returns before the guard this
// fixture exists to pin is ever reached, so the repaired shape would pass vacuously and prove nothing.
// The guard's whole subject is a shape that is defective for a different reason.
using System;

class C {
    [ThreadStatic] static Lazy<int> Value = new(() => 1, false);
}
