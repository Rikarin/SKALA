// A constructor has no return type to write; the instance it makes is not a return value that
// any documentation generator renders.
public sealed class Point {
    /// <summary>Creates a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <returns>A new point.</returns>
    public Point(int x) => X = x;

    /// <summary>The horizontal coordinate.</summary>
    public int X { get; }
}
