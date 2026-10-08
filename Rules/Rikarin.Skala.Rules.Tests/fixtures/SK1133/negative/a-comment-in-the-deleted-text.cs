// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Noted {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source /* snapshot */.ToArray();
        return copied;
    }
}
