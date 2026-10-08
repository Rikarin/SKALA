// fixture-option: LangVersion = latest
// fixture-option: TargetFramework = net9.0
// ⚠ #515: SDK 9.0.1xx builds net9.0 and ships Roslyn 4.12, whose `latest` is C# 13 and whose lowering is
// not measured to be the call. System.Runtime 9.0.0.0 proves nothing about the compiler; the same file on
// net10.0 references is a positive.
using System.Collections.Generic;
using System.Linq;

public sealed class NineLatest {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
