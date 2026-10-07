// `with` on a struct capture builds a copy; the initializer's assignment is to the copy's member.
namespace Fixtures {
    struct Point {
        public int X;
        public int Y;
    }

    struct Frame(Point origin) {
        public Point Shifted(int x) => origin with { X = x };

        public int Y => origin.Y;
    }
}
