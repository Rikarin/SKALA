using System.Collections.Immutable;
using System.Linq;

public sealed class Registry {
    // The same exception-type change as an array: ImmutableArray's indexer is the array's.
    public static int Third(ImmutableArray<int> entries) => entries.ElementAt(2);
}
