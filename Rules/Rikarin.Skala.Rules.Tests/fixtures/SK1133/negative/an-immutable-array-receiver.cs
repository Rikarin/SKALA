// fixture-option: LangVersion = 14
// A struct receiver is copied through its span by the lowering, not by the call it replaces.
using System.Collections.Immutable;
using System.Linq;

public sealed class Frozen {
    public int[] Copy(ImmutableArray<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
