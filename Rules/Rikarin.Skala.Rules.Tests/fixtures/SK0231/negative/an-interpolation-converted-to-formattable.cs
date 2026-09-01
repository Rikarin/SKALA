using System;

public static class Formattable {
    // `FormattableString fs = $"a";` accepts the interpolation and rejects the literal.
    public static FormattableString Message() => $"a plain sentence";
}
