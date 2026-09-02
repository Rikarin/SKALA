interface IReader {
    int Read();
}

class ReaderBase : IReader {
    public virtual int Read() => 0;
}

class Reader : ReaderBase, IReader {
    public new int Read() => 1;
}
