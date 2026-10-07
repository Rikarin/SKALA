using System.Globalization;

// ⚠ #412's audit: `0.00m` equals `0m` and is not the same value — a decimal keeps its scale, and
// `ToString` prints it. Dropping the argument printed "0" where the call printed "0.00".
public static class Money {
    public static string Format(decimal amount = 0m) => amount.ToString(CultureInfo.InvariantCulture);
}

public static class Probe {
    public static string Run() => Money.Format(0.00m);
}
