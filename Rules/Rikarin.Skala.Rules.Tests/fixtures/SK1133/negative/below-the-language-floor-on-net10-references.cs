// fixture-option: LangVersion = 13
// fixture-option: TargetFramework = net10.0
// ⚠ #515: the net10.0 reference set proves the compiler, not the language. C# 13 is written here, and the
// floor is asked of the effective version whichever proof holds.
using System.Collections.Generic;
using System.Linq;

public sealed class TenAtThirteen {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
