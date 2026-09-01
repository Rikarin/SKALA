// Not a round trip: the operand was unsigned to begin with, so this converts a result.
public sealed class Unsigned {
    public int High(uint hash) => (int)(hash >> 16);
}
