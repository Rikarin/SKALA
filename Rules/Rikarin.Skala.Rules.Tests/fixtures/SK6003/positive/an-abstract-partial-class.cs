public abstract partial class Importer {
    public Importer(int order) {
        Order = order;
    }

    public int Order { get; }
}

public partial class Importer {
    public string Name => "importer";
}
