// #430: a predicate may branch and write its own locals and still run no code but the language's.
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool AllSmall(List<int> values) {
        return values.All(value => {
                var limit = 10;
                if (value < 0) {
                    limit = -limit;
                }

                return value < limit;
            }
        );
    }
}
