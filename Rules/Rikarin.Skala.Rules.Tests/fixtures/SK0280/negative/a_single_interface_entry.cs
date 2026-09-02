interface IReader {
    int Read();
}

class FileReader : IReader {
    public int Read() => 0;
}
