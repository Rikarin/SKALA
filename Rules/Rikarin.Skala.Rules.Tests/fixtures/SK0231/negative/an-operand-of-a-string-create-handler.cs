using System;
using System.Globalization;

public static class Bleed {
    // ⚠ The handler conversion applies to the whole `+` chain only while *every* operand is an
    // interpolated string. Drop this `$` and the expression is a `string`, the
    // `string.Create(IFormatProvider, ref DefaultInterpolatedStringHandler)` overload stops
    // applying, and the call binds to the `ref` parameter it cannot bind to — CS1620.
    public static string Describe(double bleed) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"the bleed is {bleed:F1} kg/s "
            + $"which is not what the turbine is giving up"
        );
}
