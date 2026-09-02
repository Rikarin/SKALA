using System.IO;

public static class Store {
    public static void Publish(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite
        );
}
