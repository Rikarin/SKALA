interface IReader {
    int Read();
}

interface ISeekableReader : IReader {
    void Seek(int offset);
}

interface IBoth : ISeekableReader, IReader { }
