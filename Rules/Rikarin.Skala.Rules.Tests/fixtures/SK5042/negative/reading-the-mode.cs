using System.IO;

// Asking what the mode is does not set it.
public static class Store {
    public static bool WorldWritable(string path) =>
        File.GetUnixFileMode(path).HasFlag(UnixFileMode.OtherWrite);
}
