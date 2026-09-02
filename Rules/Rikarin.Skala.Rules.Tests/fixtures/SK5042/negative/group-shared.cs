using System.IO;

// Sharing with a group is what the group bits are for, and it names who may write rather than
// letting everyone. The rule is about `OtherWrite` alone.
public static class Store {
    public static void Publish(string path) =>
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        );
}
