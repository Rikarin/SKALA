using System;
using System.IO;

public sealed class Consumer {
    static bool Configured() => true;

    public void Write(string path) {
        using (var writer = new StreamWriter(path) { AutoFlush = Configured() }) {
            writer.WriteLine("done");
        }
    }
}
