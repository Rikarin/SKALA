// A lambda that writes the capture writes the hidden field whenever it is called.
namespace Fixtures {
    sealed class Meter(int ticks) {
        public System.Action Ticker => () => ticks++;

        public int Ticks => ticks;
    }
}
