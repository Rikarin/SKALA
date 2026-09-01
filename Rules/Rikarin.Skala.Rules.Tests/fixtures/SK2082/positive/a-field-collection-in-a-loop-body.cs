using System.Collections.Generic;

public sealed class Rows {
    readonly List<int> slots = [0, 0, 0];

    public void Reset(IEnumerable<int> values) {
        foreach (var value in values) {
            slots[0] = value;
            slots[0] = -value;
        }
    }
}
