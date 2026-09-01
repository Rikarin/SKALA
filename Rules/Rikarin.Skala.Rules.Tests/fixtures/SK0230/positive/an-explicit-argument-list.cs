public sealed class Buffer {
    public Buffer(int capacity) => Capacity = capacity;

    public int Capacity { get; }

    public int Limit { get; set; }
}

public static class Buffers {
    // The arguments stay; only the braces go.
    public static Buffer Create() => new Buffer(16) { };
}
