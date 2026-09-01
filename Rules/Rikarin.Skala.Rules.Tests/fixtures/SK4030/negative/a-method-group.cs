using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // A method group converts to either delegate, so this one is a miss rather than a wrong answer.
    public static bool AnyReady(List<int> values) => values.Any(IsReady);

    static bool IsReady(int value) => value > 0;
}
