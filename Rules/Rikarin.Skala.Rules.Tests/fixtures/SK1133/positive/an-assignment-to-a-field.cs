// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Cache {
    int[] values = [];

    public void Refresh(IEnumerable<int> source) {
        values = source.ToArray();
    }

    public int Count => values.Length;
}
