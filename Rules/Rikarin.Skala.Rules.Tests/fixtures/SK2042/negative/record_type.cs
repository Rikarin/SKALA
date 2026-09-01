using System;

record Point(int X, int Y) {
    public int Extra { get; init; }

    public override int GetHashCode() => HashCode.Combine(X, Extra);
}
