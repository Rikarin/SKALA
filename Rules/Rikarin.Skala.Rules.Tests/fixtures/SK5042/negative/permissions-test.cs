using System.IO;

// A test that creates a world-writable file in order to assert something about permissions is
// real, and a security rule at `error` that breaks a test suite teaches a reviewer to skim past
// every security finding the tool makes.
public sealed class FactAttribute : System.Attribute {
}

public sealed class PermissionTests {
    [Fact]
    public void A_world_writable_file_is_detected() {
        var path = Path.GetTempFileName();
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite);
        File.Delete(path);
    }
}
