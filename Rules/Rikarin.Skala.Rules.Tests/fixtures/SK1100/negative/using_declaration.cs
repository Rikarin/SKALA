using System.IO;

public sealed class Opening {
    static MemoryStream Open() => new();

    // `using var` says the local is disposed at the end of the block. Deleting the declaration
    // deletes the disposal.
    public static Stream Borrow() {
        using var stream = Open();
        return stream;
    }
}
