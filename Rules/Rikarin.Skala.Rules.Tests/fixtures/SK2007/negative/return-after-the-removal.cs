using System.Collections.Generic;
using System.Linq;

public sealed class Taker {
    // A `return` leaves the loop as surely as a `break` does, and the rule cannot tell which path
    // reaches it, so a body containing one is not reported at all.
    public bool TakeOne(List<int> items) {
        foreach (var item in items) {
            if (item > 0) {
                items.Remove(item);
                return true;
            }
        }

        return false;
    }
}
