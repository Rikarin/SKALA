using System;

public sealed class Import {
    // `TimeSpan`'s parse reads the culture's decimal separator, and `CA1305` leaves its `TryParse`
    // uncovered in exactly the same way as the date types'.
    public bool Read(string text) => TimeSpan.TryParse(text, out _);
}
