// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// The left half of docs/plan/06's precedence: the right-hand side names the type, so `var` takes the
// declaration and target-typed `new` never gets a left-hand type to target.
public class VarWinsWhenRhsNamesTheType {
    public void Locals() {
        List<int> numbers = new List<int>();
        Dictionary<string, List<int>> index = new Dictionary<string, List<int>>();
        VarWinsWhenRhsNamesTheType self = new VarWinsWhenRhsNamesTheType();
        int[] array = new int[4];
    }

    public void BuiltIns() {
        int count = 0;
        string name = "n";
        bool flag = true;
        double ratio = 1.5;
    }

    public void NotApparent() {
        List<int> fromCall = Make();
        int fromProperty = Count;
    }

    public int Count { get; set; }

    public List<int> Make() {
        return new List<int>();
    }
}
