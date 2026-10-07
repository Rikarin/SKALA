// A writable `ref` local aliases the capture, so whatever it writes, the capture holds (CS9116).
namespace Fixtures {
    sealed class Gauge(int level) {
        public void Fill() {
            ref var slot = ref level;
            slot = 100;
        }

        public int Level => level;
    }
}
