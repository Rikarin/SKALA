using System;
using System.IO;

// The other shape. Cleanup on the way past is not a second record of the failure.
public sealed class Loader {
    public string Load(string path) {
        var handle = File.OpenRead(path);
        try {
            return new StreamReader(handle).ReadToEnd();
        } catch (IOException error) {
            Release(handle, error);
            throw;
        }
    }

    static void Release(FileStream handle, Exception cause) => handle.Dispose();
}
