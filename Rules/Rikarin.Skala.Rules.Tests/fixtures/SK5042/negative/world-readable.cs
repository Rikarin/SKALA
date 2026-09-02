using System.IO;

// ⚠ The measured reason world-readable is not a rule: plain `File.WriteAllText` already creates
// at 0644, because that is what the process umask says. A rule reporting `OtherRead` would report
// every file-writing call in existence.
public static class Store {
    public static void Publish(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead
        );
}
