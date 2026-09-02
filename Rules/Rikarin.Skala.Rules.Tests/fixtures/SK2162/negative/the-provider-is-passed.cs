using System;
using System.Globalization;

public sealed class Import {
    public bool Invariant(string text) => DateTime.TryParse(text, CultureInfo.InvariantCulture, out _);

    public bool Current(string text) => DateTime.TryParse(text, CultureInfo.CurrentCulture, out _);

    public bool Styled(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _);
}
