// ⚠ #397: a C# 14 partial constructor's two halves must state the same accessibility (CS8799), so the
// fix rewrites both `public`s. It used to rewrite the implementation's alone — the only half any
// syntax-node action is shown — and the round trip broke the build.
public abstract partial class Importer {
    public partial Importer(string name);

    public partial Importer(string name) {
        Name = name;
    }

    public string Name { get; }
}

public sealed class TextImporter : Importer {
    public TextImporter() : base("text") { }
}
