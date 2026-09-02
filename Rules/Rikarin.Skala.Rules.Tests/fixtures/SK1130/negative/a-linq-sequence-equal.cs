using System.Collections.Generic;
using System.Linq;

// A `SequenceEqual` that is not `MemoryExtensions`'. The name matches and nothing else does.
public static class Numbers {
    public static bool Same(IEnumerable<int> left, IEnumerable<int> right) => left.SequenceEqual(right);
}
