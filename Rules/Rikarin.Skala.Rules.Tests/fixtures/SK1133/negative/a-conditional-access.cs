// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Maybe {
    public int[]? Copy(IEnumerable<int>? source) {
        int[]? copied = source?.ToArray();
        return copied;
    }
}
