using System.IO;

// The stream is disposed before the log line today and would be disposed after it as a declaration.
// That is a different program, and on a file handle it is a visibly different one.
public sealed class Writer {
    public void Write(string path) {
        using (var stream = File.OpenWrite(path)) {
            stream.WriteByte(0);
        }

        System.Console.WriteLine("done");
    }
}
