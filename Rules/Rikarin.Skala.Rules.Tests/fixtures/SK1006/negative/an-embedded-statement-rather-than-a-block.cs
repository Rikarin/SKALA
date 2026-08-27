using System.IO;

public sealed class Writer {
    public void Write(string path) {
        using (var stream = File.OpenWrite(path))
            stream.WriteByte(0);
    }
}
