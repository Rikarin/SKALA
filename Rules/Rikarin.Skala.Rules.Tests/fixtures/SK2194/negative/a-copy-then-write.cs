// A local copy of a struct capture is a different variable; writing it leaves the capture alone.
namespace Fixtures {
    struct Point {
        public int X;
    }

    struct Frame(Point origin) {
        public Point Moved(int x) {
            var copy = origin;
            copy.X = x;
            return copy;
        }
    }
}
