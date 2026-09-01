using System;

sealed class Priority : IComparable<Priority> {
    public int Rank { get; init; }

    public int CompareTo(Priority? other) => other is null ? 1 : Rank.CompareTo(other.Rank);
}
