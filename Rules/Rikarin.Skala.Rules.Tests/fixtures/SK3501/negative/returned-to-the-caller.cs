using System.IO;

public sealed class Opener {
    // Ownership goes to the caller. A `using` here would hand back a closed stream.
    public Stream Open(string path) {
        var stream = new FileStream(path, FileMode.Open);
        return stream;
    }
}
