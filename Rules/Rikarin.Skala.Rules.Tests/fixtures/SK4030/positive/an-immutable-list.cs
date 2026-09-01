using System.Collections.Immutable;
using System.Linq;

public sealed class Registry {
    public static bool AnyReady(ImmutableList<int> values) => values.Any(value => value > 0);
}
