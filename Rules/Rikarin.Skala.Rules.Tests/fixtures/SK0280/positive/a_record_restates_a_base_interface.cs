interface IReader {
    int Read();
}

interface ISeekableReader : IReader {
    void Seek(int offset);
}

record Reader(int Offset) : ISeekableReader, IReader {
    public int Read() => 0;

    public void Seek(int offset) { }
}
