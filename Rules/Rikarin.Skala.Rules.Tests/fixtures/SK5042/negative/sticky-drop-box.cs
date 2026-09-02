using System.IO;

// ⚠ The escape, and the only one. `OtherWrite` with `StickyBit` is mode 1777 — what `/tmp` itself
// is — where anyone may add an entry and only its owner may remove it. That is a deliberate
// design rather than an accident, and it is said in the code.
public static class Store {
    public static void Prepare(string path) =>
        Directory.CreateDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
            | UnixFileMode.StickyBit
        );
}
