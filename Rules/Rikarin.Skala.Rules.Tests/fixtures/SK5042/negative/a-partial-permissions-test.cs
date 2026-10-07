using System.IO;

// #400: the permissions test as a partial method, `[Fact]` on the definition.
public sealed class FactAttribute : System.Attribute {
}

public sealed partial class PermissionTests {
    [Fact]
    public partial void A_world_writable_file_is_detected();

    public partial void A_world_writable_file_is_detected() {
        var path = Path.GetTempFileName();
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite);
        File.Delete(path);
    }
}
