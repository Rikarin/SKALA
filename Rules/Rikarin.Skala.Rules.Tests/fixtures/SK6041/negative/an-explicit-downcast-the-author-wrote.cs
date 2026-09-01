using System.Collections.Generic;
using System.Text;

public static class Downcast {
    public static string Join(List<object> values) {
        var builder = new StringBuilder();
        foreach (string value in values) {
            builder.Append(value);
        }

        return builder.ToString();
    }
}
