using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // On a non-generic IEnumerable the call is the only thing producing a typed sequence.
    public static IEnumerable<string> Names(IEnumerable source) => source.Cast<string>();
}
