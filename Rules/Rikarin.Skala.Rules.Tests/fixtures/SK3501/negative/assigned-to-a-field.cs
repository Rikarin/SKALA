using System.IO;

public sealed class Cache {
    Stream? _stream;

    public void Open(string path) {
        var stream = new FileStream(path, FileMode.Open);
        _stream = stream;
    }
}
