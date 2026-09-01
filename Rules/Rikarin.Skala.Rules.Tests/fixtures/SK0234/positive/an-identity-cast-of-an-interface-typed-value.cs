using System.Collections.Generic;

public static class Interfaces {
    public static IReadOnlyList<int> Same(IReadOnlyList<int> values) {
        var same = (IReadOnlyList<int>)values;
        return same;
    }
}
