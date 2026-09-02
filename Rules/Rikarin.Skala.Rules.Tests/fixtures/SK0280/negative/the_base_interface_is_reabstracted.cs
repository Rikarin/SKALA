interface IReader {
    int Read() => 0;
}

interface IStrictReader : IReader {
    abstract int IReader.Read();
}

class FileReader : IStrictReader, IReader {
    public int Read() => 1;
}
