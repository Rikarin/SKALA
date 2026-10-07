// #408: writing a field of a struct-typed capture writes the capture itself. Under `readonly struct`
// this is CS9117.
namespace Fixtures {
    struct Point {
        public int X;
    }

    struct Anchor(Point origin) {
        public void Move(int x) => origin.X = x;

        public int X => origin.X;
    }
}
