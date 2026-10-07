// `IReader` is invariant, so its position in the list settles nothing at run time: `IText` brings it
// in either way and the type implements it in the same place.
public interface IReader {
    string Read();
}

public interface IText : IReader { }

public sealed class Document : IReader, IText {
    public string Read() => "text";
}

public static class Probe {
    public static string Run() {
        IReader reader = new Document();
        return reader.Read();
    }
}
