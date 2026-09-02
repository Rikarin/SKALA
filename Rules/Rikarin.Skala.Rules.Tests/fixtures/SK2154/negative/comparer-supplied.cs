// The ordering is supplied, which is the whole of what the rule asks for. Every overload here takes
// a comparer or a comparison, so the fallback to `Comparer<T>.Default` never happens.
using System;
using System.Collections.Generic;
using System.Linq;

sealed class Point {
    public int X { get; set; }
}

sealed class ByX : IComparer<Point> {
    public int Compare(Point left, Point right) => 0;
}

class C {
    void WithComparer(List<Point> points) => points.Sort(new ByX());
    void WithComparison(List<Point> points) => points.Sort((a, b) => a.X - b.X);
    void ArrayWithComparer(Point[] points) => Array.Sort(points, new ByX());
    IEnumerable<Point> OrderedWithComparer(IEnumerable<Point> points) => points.OrderBy(p => p, new ByX());
    IEnumerable<Point> ByField(IEnumerable<Point> points) => points.OrderBy(p => p.X);
}
