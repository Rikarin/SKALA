// A `goto` can jump backwards into the body, and chasing those cycles is not something this
// analysis does, so any `goto` withdraws the finding.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            retry:
            if (item > 0) {
                goto retry;
            }

            return;
        }
    }
}
