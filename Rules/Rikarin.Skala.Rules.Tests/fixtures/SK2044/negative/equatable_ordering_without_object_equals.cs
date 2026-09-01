// ⚠ This is the one shape the SK2004 gate actually holds off, and it took a sabotage to find it.
// The typed-Equals half is mutually exclusive with SK2004 by its own condition, so the neighbouring
// fixture passes whether or not the gate exists. Only the ordering half can reach a type that
// SK2004 also reports: `IEquatable<Self>` and no `Equals(object)` — which is SK2004's finding —
// together with `==`, `IComparable<Self>` and no relational operators, which is this rule's.
using System;

sealed class Revision : IComparable<Revision>, IEquatable<Revision> {
    public int Number { get; init; }

    public static bool operator ==(Revision? left, Revision? right) => Equals(left, right);

    public static bool operator !=(Revision? left, Revision? right) => !(left == right);

    public int CompareTo(Revision? other) => other is null ? 1 : Number.CompareTo(other.Number);

    public bool Equals(Revision? other) => other is not null && other.Number == Number;
}
