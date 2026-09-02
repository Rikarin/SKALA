using System;

public sealed class Wide {
    public int Sum() {
        var row = Tuple.Create(1, 2, 3, 4, 5, 6, 7);
        return row.Item1 + row.Item2 + row.Item3 + row.Item4 + row.Item5 + row.Item6 + row.Item7;
    }
}
