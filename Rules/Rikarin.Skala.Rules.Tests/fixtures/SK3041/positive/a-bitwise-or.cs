public sealed class Flags {
    volatile int state;

    public void Raise(int bit) {
        // `|=` is no more atomic than `+=`: a concurrent `Raise` can drop the other bit entirely.
        state |= bit;
    }

    public int State => state;
}
