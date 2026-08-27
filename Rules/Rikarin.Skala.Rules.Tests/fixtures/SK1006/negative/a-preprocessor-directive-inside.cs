using System.IO;

public sealed class Writer {
    public void Write(string path) {
        using (var stream = File.OpenWrite(path)) {
#if DEBUG
            stream.WriteByte(1);
#else
            stream.WriteByte(0);
#endif
        }
    }
}
