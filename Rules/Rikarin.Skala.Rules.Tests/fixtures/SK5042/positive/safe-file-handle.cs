using System.IO;
using Microsoft.Win32.SafeHandles;

// The handle overload of `SetUnixFileMode`. The rule names no receiver — it matches the
// `UnixFileMode`-typed argument — so both spellings arrive covered from one test.
public static class Store {
    public static void Publish(SafeFileHandle handle) =>
        File.SetUnixFileMode(
            handle,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite
        );
}
