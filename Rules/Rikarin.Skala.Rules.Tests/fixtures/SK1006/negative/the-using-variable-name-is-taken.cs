using System.IO;

public sealed class Writer {
    public void Write(string path) {
        if (path.Length > 0) {
            var stream = 1;
            System.Console.WriteLine(stream);
        }

        using (var stream = File.OpenWrite(path)) {
            stream.WriteByte(0);
        }
    }
}
