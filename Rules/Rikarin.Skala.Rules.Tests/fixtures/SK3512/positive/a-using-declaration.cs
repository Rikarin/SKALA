using System.IO;

public sealed class Loader {
    public Stream Open(string path) {
        using var stream = File.OpenRead(path);
        return stream;
    }
}
