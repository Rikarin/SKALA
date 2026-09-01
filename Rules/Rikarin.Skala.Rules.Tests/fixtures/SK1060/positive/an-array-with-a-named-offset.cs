public sealed class Window {
    public byte Sample(byte[] buffer, int back) => buffer[buffer.Length - back];
}
