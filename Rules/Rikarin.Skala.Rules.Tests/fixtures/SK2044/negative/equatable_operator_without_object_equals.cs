// ⚠ This is the one shape the SK2004 gate actually holds off, and it took a sabotage to find it:
// with `IEquatable<Self>` present the typed-Equals sub-case is mutually exclusive with SK2004 by
// its own condition, so the neighbouring fixture passes whether or not the gate exists. Only an
// `operator ==` *and* `IEquatable<Self>` *and* no `Equals(object)` reaches both rules at once.
using System;

sealed class Handle : IEquatable<Handle> {
    public int Id { get; init; }

    public static bool operator ==(Handle? left, Handle? right) => left?.Id == right?.Id;

    public static bool operator !=(Handle? left, Handle? right) => !(left == right);

    public bool Equals(Handle? other) => other is not null && other.Id == Id;
}
