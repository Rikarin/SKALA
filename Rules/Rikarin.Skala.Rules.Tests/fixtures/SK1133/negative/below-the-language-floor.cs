// fixture-option: LangVersion = 13
// C# 13 compiles `[..source]`, but on Roslyn 4.12 to 4.14, and 4.11's lowering is not the call.
using System.Collections.Generic;
using System.Linq;

public sealed class Thirteen {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
