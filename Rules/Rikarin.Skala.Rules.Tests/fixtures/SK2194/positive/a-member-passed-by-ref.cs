// A struct capture's field passed by `ref` is the capture's storage handed out for writing (CS9119).
namespace Fixtures {
    struct Point {
        public int X;
    }

    struct Probe(Point at) {
        public void Clear() => Zero(ref at.X);

        public int X => at.X;

        static void Zero(ref int target) => target = 0;
    }
}
