// A property called `IV` on a type that is not a cipher. The rule matches the declaring type rather
// than the name.
public sealed class Waveform {
    public byte[] IV { get; set; } = new byte[16];

    public void Reset() => IV = new byte[] { 1, 2, 3, 4 };
}
