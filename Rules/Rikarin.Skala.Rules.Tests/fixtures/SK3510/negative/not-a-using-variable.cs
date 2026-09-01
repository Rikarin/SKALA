using System.IO;

public sealed class Reader {
    // The local owns the stream itself, so the explicit Dispose is the only one there is.
    public int Read(string path) {
        var stream = new FileStream(path, FileMode.Open);
        var first = stream.ReadByte();
        stream.Dispose();
        return first;
    }
}
