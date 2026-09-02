// The `break` ends the inner loop, and the outer one carries on. The region analysis binds it to
// the inner loop, so it never counts as an exit from the outer body.
//
// ⚠ The inner `break` is under a condition on purpose. Writing it unconditionally made this file a
// *positive* fixture for the inner loop while still being a negative one for the outer, and the rule
// reported it — correctly. The fixture was wrong, not the rule.
using System.Collections.Generic;

class C {
    void M(List<List<int>> groups) {
        foreach (var group in groups) {
            foreach (var item in group) {
                if (item > 0) {
                    break;
                }
            }
        }
    }
}
