using System.Collections.Generic;

namespace Fixtures {
    struct Coordinate {
        public double X;
        public double Y;
    }

    sealed class Visited {
        readonly HashSet<Coordinate> seen = new HashSet<Coordinate>();

        public bool Mark(Coordinate coordinate) => seen.Add(coordinate);
    }
}
