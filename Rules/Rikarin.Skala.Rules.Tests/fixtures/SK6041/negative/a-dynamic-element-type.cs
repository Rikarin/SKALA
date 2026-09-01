using System.Collections.Generic;
using System.Text;

public static class Loose {
    public static string Join(List<dynamic> values) {
        var builder = new StringBuilder();
        foreach (object value in values) {
            builder.Append(value);
        }

        return builder.ToString();
    }
}
