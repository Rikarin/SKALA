using System.Diagnostics;

// Records and structs are examined by the same code path; these name members that exist.
[DebuggerDisplay("{Name,nq}")]
sealed record Person(string Name);

[DebuggerDisplay("{X}")]
readonly struct Point {
    public int X { get; init; }
}
