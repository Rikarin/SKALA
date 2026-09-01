using System;

sealed class Handle : IEquatable<Handle> {
    public int Id { get; init; }

    public bool Equals(Handle? other) => other is not null && other.Id == Id;
}
