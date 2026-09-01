namespace Contoso.Design;

public interface IImporter {
    int Import(string path);
}

public abstract class ImporterBase : IImporter {
    public string Name { get; init; } = string.Empty;

    // Left unimplemented on purpose: a derived type must supply it, and that obligation lives in the
    // base list rather than in this declaration.
    public abstract int Import(string path);
}

// A middle-of-hierarchy class that adds nothing abstract of its own is exempt through its base list.
public abstract class BufferedImporterBase : ImporterBase {
    public int BufferSize { get; init; } = 4096;
}
