namespace Contoso.Design;

// Never copied into anything compiled separately, so there is nothing to go stale.
public static class Limits {
    internal const int MaxRetries = 3;

    const string Marker = "x";

    private const double Ratio = 0.5;

    public static string Describe() => Marker + MaxRetries + Ratio;
}
