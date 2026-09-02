using System.Collections.Immutable;
using System.Linq;

public sealed class Registry {
    // ⚠ This does not decline for the reason it looks like it declines for, and the sabotage that
    // added ImmutableArray<T> to the receiver set turned nothing red until that was understood.
    // `System.Linq.ImmutableArrayExtensions` declares its own `ElementAt(this ImmutableArray<T>, int)`,
    // so the call never binds to `Enumerable.ElementAt` at all and the "must be Enumerable's" guard
    // is what refuses it — the receiver set is never consulted. The exception argument would exclude
    // it too, one guard later: ImmutableArray's indexer is the array's and throws
    // IndexOutOfRangeException where ElementAt throws ArgumentOutOfRangeException.
    public static int Third(ImmutableArray<int> entries) => entries.ElementAt(2);
}
