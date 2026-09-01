// The same promotion, one width down.
public sealed class Bytes {
    public sbyte High(sbyte value, int bits) => (sbyte)((byte)value >> bits);
}
