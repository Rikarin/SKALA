// `Math.Max(x, x)` and `a.CompareTo(a)` are invocations, not operators, and are outside the
// subject entirely. Generated code emits both.
using System;

class C {
    int M(int x) => Math.Max(x, x);

    int N(string a) => string.CompareOrdinal(a, a);
}
