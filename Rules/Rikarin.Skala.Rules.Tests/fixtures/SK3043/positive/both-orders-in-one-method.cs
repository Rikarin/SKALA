public sealed class Buffer {
    static readonly object Read = new();

    static readonly object Write = new();

    static int depth;

    public static void Swap(bool forward) {
        if (forward) {
            lock (Read) {
                lock (Write) {
                    depth++;
                }
            }
        } else {
            lock (Write) {
                lock (Read) {
                    depth--;
                }
            }
        }
    }
}
