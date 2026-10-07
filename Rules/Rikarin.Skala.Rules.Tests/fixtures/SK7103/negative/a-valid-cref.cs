/// <summary>A reader.</summary>
public class Reader {
    /// <summary>Reads one record.</summary>
    /// <returns>The record.</returns>
    public string Read() => string.Empty;
}

/// <summary>A buffered reader.</summary>
public sealed class BufferedReader : Reader {
    /// <inheritdoc cref="Reader.Read" />
    public new string Read() => "buffered";
}
