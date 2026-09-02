using System.IO;

// No constant value, so nothing is decided here. Reporting this would mean following `mode`
// through a branch, which is the analysis doc 08 puts out of scope.
public static class Store {
    public static void Publish(string path, bool shared) {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if (shared) {
            mode |= UnixFileMode.GroupRead;
        }

        File.SetUnixFileMode(path, mode);
    }
}
