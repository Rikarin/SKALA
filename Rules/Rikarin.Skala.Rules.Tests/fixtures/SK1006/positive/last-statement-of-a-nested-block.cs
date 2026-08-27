using System.IO;

// The `using` block's closing brace and the `if` block's closing brace are the same program point,
// so the declaration disposes at exactly the same instant.
public sealed class Writer {
    public void Write(string path, bool enabled) {
        if (enabled) {
            using (var stream = File.OpenWrite(path)) {
                stream.WriteByte(0);
            }
        }
    }
}
