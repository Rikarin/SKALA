// The eighth element is `TRest`, whose nesting the fix does not reproduce.
using System;

public sealed class TooWide {
    public int Sum() {
        var row = Tuple.Create(1, 2, 3, 4, 5, 6, 7, 8);
        return row.Item1 + row.Rest.Item1;
    }
}
