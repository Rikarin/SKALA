using System.IO;

public sealed class Loader {
    // ⚠ `reader.ReadToEnd()` is read out of the resource, not the resource itself. Some of these
    // are the same bug and proving which needs to know what the member did with the object; the
    // rule reports only the case it can prove.
    public string Read(string path) {
        using var reader = new StreamReader(path);
        return reader.ReadToEnd();
    }
}
