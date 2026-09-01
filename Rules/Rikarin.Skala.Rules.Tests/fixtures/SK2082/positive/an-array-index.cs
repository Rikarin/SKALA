public sealed class Header {
    public static void Write(byte[] buffer, byte version) {
        buffer[0] = 1;
        buffer[1] = 2;
        buffer[0] = version;
    }
}
