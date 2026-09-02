interface IReader {
    int Read();
}

class ReaderBase { }

class Reader : ReaderBase, IReader {
    public int Read() => 0;
}
