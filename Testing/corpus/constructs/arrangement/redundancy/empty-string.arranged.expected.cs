// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-09-04
namespace Skala.Corpus.Arrangement;

// ⚠ SK-DIV-0013: the oracle normalises `String.Empty` to `string.Empty` and stops there. Skala
// applies `resharper_empty_string = empty_literal` and produces `""`. Pinned by
// ArrangementRuleTests rather than by the fixture beside it.
public class EmptyString {
    string _field = string.Empty;

    public string Property { get; set; } = string.Empty;

    public void Locals() {
        var a = string.Empty;
        var b = string.Empty;
        var c = "";
        Console.WriteLine(a + b + c);
    }

    public void Arguments() {
        Console.WriteLine(string.Empty);
        Take(string.Empty);
    }

    // A constant context: `""` is a constant too, so the rewrite is legal here.
    public const string Constant = "";

    public void Take(string value) {
        if (value == string.Empty) {
            Console.WriteLine(_field + Property);
        }
    }
}
