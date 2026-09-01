public sealed class Packer {
    public static uint Pack(uint bits, uint value) {
        bits = bits << 4;
        return bits | value;
    }
}
