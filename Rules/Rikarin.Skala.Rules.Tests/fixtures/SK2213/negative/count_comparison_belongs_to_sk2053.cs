// ⚠ The neighbouring rules, on the same shape of comparison. `values.Count > 0` is a size
// question and SK2053's, `someByte >= 0` is a type-range question and SK2001's, and neither is an
// `IndexOf`. The three can never report the same expression: this rule's extra fact is the search
// contract that absence is `-1`, which is the carve-out SK2053's own false-positive note names.
using System.Collections.Generic;

class C {
    bool Any(List<int> values) => values.Count > 0;

    bool NonNegative(byte value) => value > 0;
}
