using System.Collections.Generic;
using System.IO;

public sealed class Lines {
    // An iterator's locals live as long as the enumerator, which is not this method's scope, so
    // "end of scope" does not mean what the fix would make it mean.
    public IEnumerable<int> Read(string path) {
        var stream = new FileStream(path, FileMode.Open);
        var first = stream.ReadByte();
        yield return first;
    }
}
