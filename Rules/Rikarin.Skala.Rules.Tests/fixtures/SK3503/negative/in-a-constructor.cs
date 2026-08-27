using System.IO;

public sealed class Snapshot {
    public Snapshot(Stream target) {
        using (var writer = new StreamWriter(target)) {
            writer.WriteLine("created");
        }
    }
}
