interface IReader {
    int Read();
}

interface ISeekableReader : IReader {
    void Seek(int offset);
}

class FileReader : ISeekableReader, IReader {
    public int Read() => 0;

    public void Seek(int offset) { }
}
