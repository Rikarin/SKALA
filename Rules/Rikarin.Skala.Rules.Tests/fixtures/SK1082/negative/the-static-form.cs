using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // There is no receiver in front of the call to turn into an indexed expression.
    public static int Third(List<int> entries) => Enumerable.ElementAt(entries, 2);
}
