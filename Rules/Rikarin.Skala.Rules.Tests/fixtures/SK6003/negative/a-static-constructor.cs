public abstract class Importer {
    static Importer() {
        Default = "text";
    }

    protected Importer() { }

    public static string Default { get; }
}
