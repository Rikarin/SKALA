interface IReader {
    int Read();
}

interface IWriter {
    void Write(int value);
}

class Pipe : IReader, IWriter {
    public int Read() => 0;

    public void Write(int value) { }
}
