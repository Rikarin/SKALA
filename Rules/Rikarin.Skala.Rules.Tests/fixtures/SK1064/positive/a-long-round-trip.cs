public sealed class Mixing {
    public long Fold(long state, int bits) => (long)((ulong)state >> bits);
}
