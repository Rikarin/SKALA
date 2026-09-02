using System.IO;

// Whether a mode assembled elsewhere ends up world-writable is a question about another method,
// so the rule declines rather than guesses.
public static class Store {
    public static void Publish(string path, UnixFileMode mode) => File.SetUnixFileMode(path, mode);
}
