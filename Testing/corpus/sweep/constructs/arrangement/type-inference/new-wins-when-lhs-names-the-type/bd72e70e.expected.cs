// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// The right half of the precedence: `var` cannot reach a field, a return, or a property initialiser,
// so target-typed `new` is what applies there.
public class NewWinsWhenLhsNamesTheType {
    readonly List<int> _field = new List<int>();

    static readonly Dictionary<string, int> Table = new Dictionary<string, int>();

    public List<string> Property { get; } = new List<string>();

    public List<int> Make() => new List<int>();

    public List<int> Expression() => new List<int>();

    public void Assignment() {
        List<int> local;
        local = new();
        Held = new();
    }

    public List<int> Held { get; set; }
}
