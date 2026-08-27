using System.IO;

public sealed class Parser {
    // ⚠ The guard that makes the fix safe rather than merely plausible. `new StreamReader(stream)`
    // takes ownership of `stream`, so a `using` on the reader would close a stream the caller is
    // still holding.
    public int Count(Stream stream) {
        var reader = new StreamReader(stream);
        var line = reader.ReadLine();
        return line?.Length ?? 0;
    }
}
