using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // A real operation: this one can throw InvalidCastException.
    public static IEnumerable<string> Names(IEnumerable<object> source) => source.Cast<string>();
}
