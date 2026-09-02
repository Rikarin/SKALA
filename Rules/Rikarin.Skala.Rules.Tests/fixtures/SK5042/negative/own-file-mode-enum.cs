namespace Vendor.Io;

// An enum of the caller's own spelled `UnixFileMode`. The rule resolves `System.IO.UnixFileMode`
// through the compilation rather than comparing names.
[System.Flags]
public enum UnixFileMode {
    None = 0,
    OtherWrite = 2,
    UserWrite = 128,
    UserRead = 256
}

public static class Store {
    public static UnixFileMode Publish() => UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite;
}
