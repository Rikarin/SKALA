using System.IO;

public sealed class Reader {
    // The repaired form.
    public int Read(string path) {
        using var stream = new FileStream(path, FileMode.Open);
        return stream.ReadByte();
    }
}
