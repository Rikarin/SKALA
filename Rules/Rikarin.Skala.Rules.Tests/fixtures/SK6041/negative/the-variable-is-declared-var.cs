using System.Collections.Generic;
using System.Text;

public static class Inferred {
    public static string Join(List<string> names) {
        var builder = new StringBuilder();
        foreach (var name in names) {
            builder.Append(name);
        }

        return builder.ToString();
    }
}
