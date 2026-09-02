interface IReader {
    int Read() => 0;
}

interface ISeekableReader : IReader {
    void Seek(int offset);
}

class FileReader : ISeekableReader, IReader {
    public void Seek(int offset) { }
}
