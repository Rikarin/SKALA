// ⚠ #425: `d.ToString()` renders in the current culture before the handler sees it, and `{d}` is
// rendered by the handler with the provider it was given. Measured for #412's audit under de-DE: `1,5`
// before the fix and `1.5` after it, for both `string.Create` and `FormattableString.Invariant`. The
// culture is built by hand because the test host may run with invariant globalization.
using System;
using System.Globalization;

public static class Probe {
    public static string Run() {
        var previous = CultureInfo.CurrentCulture;
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        CultureInfo.CurrentCulture = comma;
        try {
            var d = 1.5;
            return string.Create(CultureInfo.InvariantCulture, $"a {d.ToString()}")
                + "/"
                + FormattableString.Invariant($"b {d.ToString()}");
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
