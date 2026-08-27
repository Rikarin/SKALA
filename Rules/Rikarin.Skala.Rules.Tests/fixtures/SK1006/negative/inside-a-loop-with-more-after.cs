using System.IO;

public sealed class Writer {
    public void Write(string[] paths) {
        foreach (var path in paths) {
            using (var stream = File.OpenWrite(path)) {
                stream.WriteByte(0);
            }

            System.Console.WriteLine(path);
        }
    }
}
