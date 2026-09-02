// Object creation carries an argument list too, and a transposed pair of coordinates is the
// canonical version of this defect.
namespace Fixtures {
    sealed class Point {
        public Point(int width, int height) { }
    }

    sealed class Builder {
        public Point Build(int width, int height) => new Point(height, width);
    }
}
