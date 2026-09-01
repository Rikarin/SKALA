using System;
using System.IO;

// Handled here, recorded once, and the caller gets a result rather than an exception. This is one
// of the two shapes the rule is asking for.
public sealed class Loader {
    public string Load(string path) {
        try {
            return File.ReadAllText(path);
        } catch (IOException error) {
            Console.Error.WriteLine(error);
            return string.Empty;
        }
    }
}
