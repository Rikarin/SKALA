using System.Collections;
using System.Text;

public static class Legacy {
    public static string Join(ArrayList values) {
        var builder = new StringBuilder();
        foreach (object value in values) {
            builder.Append(value);
        }

        return builder.ToString();
    }
}
