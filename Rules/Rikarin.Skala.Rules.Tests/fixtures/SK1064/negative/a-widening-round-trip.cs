// The two casts are not the same width, so this is a widening conversion of a shifted value and
// not a round trip at all.
public sealed class Widening {
    public long High(int hash) => (long)((uint)hash >> 16);
}
