public abstract class Importer {
    protected Importer(string name) {
        Name = name;
    }

    public string Name { get; }
}
