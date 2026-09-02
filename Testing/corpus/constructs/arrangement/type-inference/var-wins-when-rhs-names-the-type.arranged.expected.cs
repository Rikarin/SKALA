// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// The left half of docs/plan/06's precedence: the right-hand side names the type, so `var` takes the
// declaration and target-typed `new` never gets a left-hand type to target.
public class VarWinsWhenRhsNamesTheType {
    public void Locals() {
        var numbers = new List<int>();
        var index = new Dictionary<string, List<int>>();
        var self = new VarWinsWhenRhsNamesTheType();
        var array = new int[4];
    }

    public void BuiltIns() {
        var count = 0;
        var name = "n";
        var flag = true;
        var ratio = 1.5;
    }

    public void NotApparent() {
        var fromCall = Make();
        var fromProperty = Count;
    }

    public int Count { get; set; }

    public List<int> Make() => new();
}
