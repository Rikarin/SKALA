using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool AllReady(List<int> values) {
        return values.All(value => {
                return value > 0;
            }
        );
    }
}
