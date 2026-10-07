using System;

// Roslyn's expansion ignores a struct's interfaces; generators that resolve `<inheritdoc/>`
// themselves do not, and between the two the rule takes the quiet answer.
/// <inheritdoc />
public readonly struct Version : IComparable<Version> {
    /// <inheritdoc />
    public int CompareTo(Version other) => 0;
}
