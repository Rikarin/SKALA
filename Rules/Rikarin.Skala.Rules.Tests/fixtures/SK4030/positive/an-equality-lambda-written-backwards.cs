using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool Knows(List<int> codes, int wanted) => codes.Any(code => wanted == code);
}
