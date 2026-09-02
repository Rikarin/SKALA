using System.IO;

public sealed record Session(string Name) {
    readonly MemoryStream buffer = new();

    public void Append(byte value) {
        buffer.WriteByte(value);
    }

    public void Dispose() {
        buffer.Dispose();
    }
}
