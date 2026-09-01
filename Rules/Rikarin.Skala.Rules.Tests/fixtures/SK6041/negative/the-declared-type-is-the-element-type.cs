using System.Collections.Generic;
using System.Text;

public static class Exact {
    public static string Join(List<string> names) {
        var builder = new StringBuilder();
        foreach (string name in names) {
            builder.Append(name);
        }

        return builder.ToString();
    }
}
