using System.IO;

// The correct shape: nobody but the owner may read or write it.
public static class Store {
    public static void Publish(string path) =>
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
}
