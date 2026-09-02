using System.IO;

// The property-assignment form, inside an object initialiser.
public static class Store {
    public static FileStream Open(string path) =>
        new FileStream(
            path,
            new FileStreamOptions {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite
            }
        );
}
