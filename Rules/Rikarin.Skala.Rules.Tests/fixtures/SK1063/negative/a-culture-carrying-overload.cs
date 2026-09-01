using System.Globalization;

// The provider is the point of this overload; interpolation would take the current culture.
public sealed class Invariant {
    public string Line(decimal amount) =>
        string.Format(CultureInfo.InvariantCulture, "{0:N2}", amount);
}
