using System.Collections.Generic;
using System.Linq;

public sealed class Holder {
    public static bool Both(List<int> left, List<int> right) => left.Any() && right.Any();
}
