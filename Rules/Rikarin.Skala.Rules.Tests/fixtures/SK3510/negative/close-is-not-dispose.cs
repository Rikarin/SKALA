using System.IO;

public sealed class Writer {
    // `Close` is a different method and is not always an alias for `Dispose`; deleting it is not
    // the same edit and the rule does not offer it.
    public void Write(string path) {
        using var stream = new FileStream(path, FileMode.Create);
        stream.WriteByte(1);
        stream.Close();
    }
}
