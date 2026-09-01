sealed class Buffer {
    readonly byte[] data = new byte[16];

    public int Length => data.Length;
}
