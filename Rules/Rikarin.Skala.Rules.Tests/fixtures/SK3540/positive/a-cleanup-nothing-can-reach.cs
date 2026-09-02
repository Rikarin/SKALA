using System.IO;

public sealed class Journal {
    readonly MemoryStream file = new();

    public void Write(byte value) {
        file.WriteByte(value);
    }

    public void Dispose() {
        file.Dispose();
    }
}
