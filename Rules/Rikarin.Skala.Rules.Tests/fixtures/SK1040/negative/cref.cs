using System;

/// <summary>A documentation reference to <see cref="Nullable{T}" /> is a name, not a use.</summary>
public sealed class Documented {
    /// <summary>Reads a <see cref="Nullable{Int32}" /> value.</summary>
    public int Read(int? value) => value ?? 0;
}
