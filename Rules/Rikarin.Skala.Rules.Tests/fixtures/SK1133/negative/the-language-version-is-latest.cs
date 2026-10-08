// fixture-option: LangVersion = latest
// ⚠ `latest` is whatever the building compiler says: C# 14 to Skala, C# 12 to Roslyn 4.8, whose lowering
// changes the exception a null receiver throws. Only a written number proves the compiler — or a .NET 10
// reference set (#515), and this one is the test host's own runtime assemblies, whose core library is
// System.Private.CoreLib: the shape of `--load=loose`, where nothing was built and nothing is proved.
using System.Collections.Generic;
using System.Linq;

public sealed class Latest {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
