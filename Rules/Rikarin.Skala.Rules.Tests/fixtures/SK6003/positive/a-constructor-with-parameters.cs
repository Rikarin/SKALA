public abstract class Importer {
    public Importer(string name) {
        Name = name;
    }

    public string Name { get; }
}

public sealed class TextImporter : Importer {
    public TextImporter() : base("text") { }
}
