// Measured: Roslyn's expansion follows an override or an interface, never a `new`, so this
// renders blank even though the hidden member is documented. `cref` is the way to name it.
/// <summary>A reader.</summary>
public class Reader {
    /// <summary>Reads one record.</summary>
    /// <returns>The record.</returns>
    public string Read() => string.Empty;
}

/// <summary>A buffered reader.</summary>
public sealed class BufferedReader : Reader {
    /// <inheritdoc />
    public new string Read() => "buffered";
}
