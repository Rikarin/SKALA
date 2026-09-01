using System.Collections.Generic;
using System.Text;

public static class Boxed {
    public static string Join(List<int> numbers) {
        var builder = new StringBuilder();
        foreach (object number in numbers) {
            builder.Append(number);
        }

        return builder.ToString();
    }
}
