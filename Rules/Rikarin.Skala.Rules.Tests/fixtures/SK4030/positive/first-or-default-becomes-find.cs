using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static string? Ready(List<string> names) => names.FirstOrDefault(name => name.Length > 0);
}
