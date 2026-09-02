using System.Collections.Generic;
using System.Linq;

sealed class Point {
    public int X { get; set; }
}

class C {
    void SortInPlace(List<Point> points) => points.Sort();

    IEnumerable<Point> Ordered(IEnumerable<Point> points) => points.OrderBy(p => p);

    IEnumerable<Point> Descending(IEnumerable<Point> points) => points.OrderByDescending(p => p);
}
