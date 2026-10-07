using System;
using System.Collections.Generic;

/// <summary>Compares ordinally.</summary>
public sealed class Ordinal : IEqualityComparer<string>, IComparable<Ordinal> {
    /// <inheritdoc />
    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

    /// <inheritdoc />
    public int GetHashCode(string obj) => StringComparer.Ordinal.GetHashCode(obj);

    /// <inheritdoc />
    public int CompareTo(Ordinal? other) => 0;
}
