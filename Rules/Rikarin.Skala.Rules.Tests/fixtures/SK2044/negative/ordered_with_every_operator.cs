using System;

sealed class Revision : IComparable<Revision> {
    public int Number { get; init; }

    public static bool operator ==(Revision? left, Revision? right) => Equals(left, right);

    public static bool operator !=(Revision? left, Revision? right) => !(left == right);

    public static bool operator <(Revision? left, Revision? right) => Compare(left, right) < 0;

    public static bool operator >(Revision? left, Revision? right) => Compare(left, right) > 0;

    public static bool operator <=(Revision? left, Revision? right) => Compare(left, right) <= 0;

    public static bool operator >=(Revision? left, Revision? right) => Compare(left, right) >= 0;

    public int CompareTo(Revision? other) => other is null ? 1 : Number.CompareTo(other.Number);

    public override bool Equals(object? other) => other is Revision revision && revision.Number == Number;

    public override int GetHashCode() => Number;

    static int Compare(Revision? left, Revision? right) =>
        left is null ? (right is null ? 0 : -1) : left.CompareTo(right);
}
