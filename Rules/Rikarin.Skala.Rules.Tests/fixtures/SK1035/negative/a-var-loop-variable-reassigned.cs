// ⚠ #425: a `var` loop variable copied into a `var` local makes that local an `object` before the fix
// and the enum after it, so assigning a string to it stops compiling (CS0029), as #412's audit measured.
using System;

public enum Color {
    Red,
    Green
}

public static class Holder {
    public static string Last() {
        object result = "";
        foreach (var value in Enum.GetValues(typeof(Color))) {
            var copy = value;
            copy = "replaced";
            result = copy;
        }

        return (string)result;
    }
}
