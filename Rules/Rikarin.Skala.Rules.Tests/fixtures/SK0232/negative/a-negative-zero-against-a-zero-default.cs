using System.Globalization;

// ⚠ #412's audit: `-0.0 == 0.0`, and `1 / d` tells them apart — -Infinity against Infinity.
public static class Reciprocal {
    public static string Of(double d = 0.0) => (1 / d).ToString(CultureInfo.InvariantCulture);
}

public static class Probe {
    public static string Run() => Reciprocal.Of(-0.0);
}
