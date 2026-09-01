// ⚠ The case that makes this rule a proof rather than a pattern match. `ushort` promotes to `int`
// before the shift runs, so the value shifted is zero-extended; `x >>> n` on a `short` promotes the
// signed value first. For x = -1 and n = 4 the first gives 4095 and the second gives -1.
public sealed class Narrow {
    public short High(short value, int bits) => (short)((ushort)value >> bits);
}
