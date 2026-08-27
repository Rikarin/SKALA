using System.IO;

public sealed class Writer {
    // ⚠ The same pattern, and not reported: `await using` needs an `async` body, and making this
    // method `async` changes its signature and every caller with it. That is a refactor, not an edit.
    public void Write(Stream target) {
        using (var writer = new StreamWriter(target)) {
            writer.WriteLine("done");
        }
    }
}
