// #401: a partial constructor's comment is written on its definition, which Roslyn's driver never
// hands a syntax-node action; the `<returns>` there was unreported until the analyzers dispatched
// the definition themselves.
public sealed partial class Point {
    /// <summary>Creates a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <returns>A new point.</returns>
    public partial Point(int x);

    public partial Point(int x) => X = x;

    /// <summary>The horizontal coordinate.</summary>
    public int X { get; }
}
