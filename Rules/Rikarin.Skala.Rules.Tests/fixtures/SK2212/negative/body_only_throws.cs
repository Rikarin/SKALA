// ⚠ The guard the corpus bought. The first draft reported a body whose endpoint was unreachable
// with no exit point at all, on the reasoning that once `goto` and nested constant-condition loops
// were excluded, an always-`throw` body was the only thing left. Vixen's `GlBindingPlan.Build`
// refuted it: a `switch` expression over an *error type* — `DescriptorKind` was `CS0246` in that
// compilation — makes the endpoint unreachable too, and the loop was reported for a reason that
// cannot exist in a build that compiles. The same shape with the enum resolved is declined,
// measured on a probe.
//
// So a jump the rule can point at is now required, and the shapes below are the price: a body that
// throws on every path runs at most once for a reason of its own, and saying so with this rule's
// message would be answering a different question. The reasons an endpoint can be unreachable are
// not something this rule can enumerate, and requiring an exit point closes all of them at once.
using System;
using System.Collections.Generic;

class C {
    void AlwaysThrows(List<int> items) {
        foreach (var item in items) {
            throw new InvalidOperationException(item.ToString());
        }
    }

    void ThrowsFromEveryBranch(List<int> items) {
        foreach (var item in items) {
            if (item > 0) {
                throw new ArgumentOutOfRangeException(nameof(items));
            }

            throw new InvalidOperationException();
        }
    }
}
