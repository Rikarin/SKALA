using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool Knows(List<string> names, string wanted) => names.Any(name => name == wanted);
}
