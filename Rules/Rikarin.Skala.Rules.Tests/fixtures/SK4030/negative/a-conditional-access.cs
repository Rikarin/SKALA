using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool AnyReady(List<int>? values) => values?.Any(value => value > 0) ?? false;
}
