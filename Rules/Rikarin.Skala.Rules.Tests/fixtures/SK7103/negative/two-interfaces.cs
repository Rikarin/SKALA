// Ambiguous resolution is still resolution: whichever one a generator picks, there is text.
/// <summary>A reader.</summary>
public interface IReader {
    /// <summary>Opens for reading.</summary>
    void Open();
}

/// <summary>A writer.</summary>
public interface IWriter {
    /// <summary>Opens for writing.</summary>
    void Open();
}

/// <summary>A file.</summary>
public sealed class File : IReader, IWriter {
    /// <inheritdoc />
    public void Open() { }
}
