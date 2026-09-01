sealed class Buffer {
    readonly byte[] data = new byte[16];

    ~Buffer() { }

    public int Length => data.Length;
}
