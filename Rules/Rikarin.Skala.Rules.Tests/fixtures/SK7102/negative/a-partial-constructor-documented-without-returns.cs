// #401: the definition is read now, and a comment there with nothing a constructor cannot have is
// not reported — on either half.
public sealed partial class Point {
    /// <summary>Creates a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    public partial Point(int x);

    public partial Point(int x) => X = x;

    /// <summary>The horizontal coordinate.</summary>
    public int X { get; }
}
