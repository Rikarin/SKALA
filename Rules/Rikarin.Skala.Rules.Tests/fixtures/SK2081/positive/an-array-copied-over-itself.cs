public sealed class Buffers {
    public static void Reset(int[] buffer) {
        buffer.CopyTo(buffer, 0);
    }
}
