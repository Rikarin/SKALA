using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static string Ready(List<string> names) => names.Where(name => name.Length > 0).First();
}
