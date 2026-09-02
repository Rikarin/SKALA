// ⚠ Ordering is excluded on purpose. A comparison whose result feeds a sort is the one place
// linguistic collation is legitimately wanted, so `Compare` and `CompareTo` are never reported —
// even with the same enum member the equality fixtures are reported for.
using System;

class C {
    int Order(string a, string b) => string.Compare(a, b, StringComparison.InvariantCulture);
    int OrderLoosely(string a, string b) => string.Compare(a, b, StringComparison.InvariantCultureIgnoreCase);
}
