using System.IO;

public sealed class Reader {
    public int Read(string path) {
        using (var stream = new FileStream(path, FileMode.Open)) {
            var first = stream.ReadByte();
            stream.Dispose();
            return first;
        }
    }
}
