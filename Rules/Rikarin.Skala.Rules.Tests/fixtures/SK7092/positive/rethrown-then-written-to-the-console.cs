using System;
using System.IO;

// The order does not matter: both records exist by the time the frame is left.
public sealed class Loader {
    public string Load(string path) {
        try {
            return File.ReadAllText(path);
        } catch (IOException error) {
            Console.Error.WriteLine(error);
            throw;
        }
    }
}
