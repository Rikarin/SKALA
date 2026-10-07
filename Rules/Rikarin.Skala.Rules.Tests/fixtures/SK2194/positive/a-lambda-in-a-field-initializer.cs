// ⚠ An initializer is construction, but a lambda inside one runs whenever it is called. Measured for
// #408: once a member captures `ticks`, calling `tick` after construction moves `Ticks`, because every
// reference to the parameter, the initializer's included, is the hidden field. Before #408 the
// expression-bodied form here was missed and a block-bodied one was not.
namespace Fixtures {
    sealed class Meter(int ticks) {
        readonly System.Action tick = () => ticks++;

        public void Tick() => tick();

        public int Ticks => ticks;
    }
}
