using System.IO;

// Only one half is in this tree, and the base list may be on the other one.
public partial class Importer {
    readonly MemoryStream source = new();

    public void Dispose() {
        source.Dispose();
    }
}
