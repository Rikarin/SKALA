using System.IO;

// No receiver is named by the rule: it matches the `UnixFileMode`-typed argument wherever it is
// passed, so `Directory.CreateDirectory`'s mode overload arrives covered.
public static class Store {
    public static void Prepare(string path) =>
        Directory.CreateDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
        );
}
