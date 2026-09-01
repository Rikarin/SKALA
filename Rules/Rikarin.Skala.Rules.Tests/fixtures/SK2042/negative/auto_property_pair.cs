using System;

sealed class Pair {
    public int Left { get; init; }

    public int Right { get; init; }

    public override bool Equals(object? other) => other is Pair pair && pair.Left == Left && pair.Right == Right;

    public override int GetHashCode() => HashCode.Combine(Left, Right);
}
