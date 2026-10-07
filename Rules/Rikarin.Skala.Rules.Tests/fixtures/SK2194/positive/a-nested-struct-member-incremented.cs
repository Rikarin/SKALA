// A chain of struct members is still the capture's storage, and `++` on it is a write (CS9117).
namespace Fixtures {
    struct Point {
        public int X;
    }

    struct Segment {
        public Point Start;
    }

    sealed class Track(Segment segment) {
        public void Nudge() => segment.Start.X++;

        public int X => segment.Start.X;
    }
}
