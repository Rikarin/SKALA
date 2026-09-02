// Nothing to decide: the rule proves an access throws from the constants on the page, and there are
// none here.
using System.Collections.Generic;

class C {
    int At(int[] values, int i) => values[i];

    int Computed(List<int> values, int i) => values[i - 1];

    int[] Range(int[] values, int start, int end) => values[start..end];
}
