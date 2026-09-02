using System.IO;

// Decided on the constant value the compiler already folded, so a named constant holding the same
// bits is one case rather than another syntax shape to enumerate.
public static class Store {
    const UnixFileMode Shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite;

    public static void Publish(string path) => File.SetUnixFileMode(path, Shared);
}
