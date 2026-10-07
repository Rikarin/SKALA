// A `ref readonly` local reads, whether bound directly, through a conditional's arms or by
// reassignment. Each compiles under `readonly struct`.
namespace Fixtures {
    struct Bounds(int low, int high) {
        public int Pick(bool upper) {
            ref readonly var direct = ref low;
            ref readonly var chosen = ref upper ? ref high : ref low;
            var scratch = 0;
            ref readonly var moved = ref scratch;
            moved = ref high;
            return direct + chosen + moved;
        }
    }
}
