using System.Collections.Generic;
using System.Text;

public static class Names {
    public static string Join(List<string> names) {
        var builder = new StringBuilder();
        foreach (object name in names) {
            builder.Append(name);
        }

        return builder.ToString();
    }
}
