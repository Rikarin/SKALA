// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-09-04
// csharp_style_namespace_declarations = file_scoped. ⚠ The oracle performs this under
// `ArrangeNamespaces`, a cleanup task the M4 profile sweep did not find; until it was added the
// reference tool left every block-scoped namespace alone.

namespace Skala.Corpus.Arrangement.Namespaces;

public class Converted {
    public int Value { get; set; }

    public void Write() {
        Console.WriteLine(Value);
    }
}

public class AlsoConverted {
    // Both types move out with the namespace; the members are not otherwise touched, and the
    // indentation that the conversion makes wrong is the formatter's to fix afterwards.
    public string Name => "x";
}
