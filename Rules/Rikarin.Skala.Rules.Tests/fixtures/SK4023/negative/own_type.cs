static class OwnTypeFixture {
    public static Buffer Make() => new Buffer(0);
}

sealed class Buffer {
    public Buffer() { }

    public Buffer(int capacity) => Capacity = capacity;

    public int Capacity { get; }
}
