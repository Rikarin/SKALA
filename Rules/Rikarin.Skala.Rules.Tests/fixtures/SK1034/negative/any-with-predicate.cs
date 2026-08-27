using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public static bool HasEven(List<int> items) => items.Any(item => item % 2 == 0);
}
