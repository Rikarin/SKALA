using System;
using System.Globalization;

public static class Plain {
    // ⚠ A *single* `$""` with no interpolations anywhere in the call is load-bearing for the same
    // reason. The exclusion is about the target type, not about whether siblings interpolate.
    public static string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"a plain sentence");
}
