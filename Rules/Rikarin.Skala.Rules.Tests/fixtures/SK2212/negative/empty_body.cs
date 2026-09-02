// A loop with no statements has no jump in it, and an empty loop is a different finding.
//
// ⚠ The explicit empty-body guard is not what saves this file — a sabotage removed it and this
// fixture stayed green. Control falls straight off the end of an empty block, so the endpoint is
// reachable and the flow test declines it one step later. The guard is kept because it says what
// the rule means and saves the analysis, not because anything depends on it.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) { }
    }
}
