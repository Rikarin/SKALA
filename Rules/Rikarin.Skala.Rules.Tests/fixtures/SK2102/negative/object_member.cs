using System.Diagnostics;

// A bare identifier naming a member of `System.Object` resolves through the base chain.
[DebuggerDisplay("{ToString}")]
sealed class Basket { }
