using System;

public sealed class Totals {
    public int Sum(int[] values) {
        Nullable<int> total = null;
        foreach (var value in values) {
            total = (total ?? 0) + value;
        }

        return total ?? 0;
    }
}
