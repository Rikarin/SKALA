using System.Collections.Generic;

public sealed class Histogram {
    readonly List<int> buckets = [0, 0, 0];

    public int this[int index] {
        get => buckets[index];
        set => buckets[index] += value;
    }
}

public sealed class Use {
    // The setter accumulates. Two writes to one index are two events, not one lost value.
    public static void Record(Histogram histogram) {
        histogram[0] = 1;
        histogram[0] = 2;
    }
}
