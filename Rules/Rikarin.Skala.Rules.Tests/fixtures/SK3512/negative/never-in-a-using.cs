using System.IO;

public sealed class Loader {
    // The caller is handed a live stream and owns it, which is the correct shape of this method.
    public Stream Open(string path) {
        var stream = File.OpenRead(path);
        return stream;
    }
}
