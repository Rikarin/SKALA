// ⚠ #425: the loop variable's type is the collection's element type, `object` over `Array` and the enum
// over `T[]`, so the call inside the loop binds another overload once the call is generic. Measured
// for #412's audit: `object Red` before the fix and `Color Red` after it.
using System;

public enum Color {
    Red,
    Green
}

public static class Probe {
    static string Show(object value) => "object " + value + ";";

    static string Show(Color value) => "Color " + value + ";";

    public static string Run() {
        var text = "";
        foreach (var value in Enum.GetValues(typeof(Color))) {
            text += Show(value);
        }

        return text;
    }
}
