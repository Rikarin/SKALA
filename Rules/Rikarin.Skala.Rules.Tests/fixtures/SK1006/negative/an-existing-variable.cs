using System.IO;

// There is no declaration to convert: `using var` needs one.
public sealed class Writer {
    public void Write(Stream stream) {
        using (stream) {
            stream.WriteByte(0);
        }
    }
}
