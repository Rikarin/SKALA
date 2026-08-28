// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

namespace Skala.Corpus.Arrangement;

// ⚠ SK-DIV-0013: the oracle normalises `String.Empty` to `string.Empty` and stops there. Skala
// applies `resharper_empty_string = empty_literal` and produces `""`. Pinned by
// ArrangementRuleTests rather than by the fixture beside it.
public class EmptyString {
    private string _field = string.Empty;

    public string Property { get; set; } = String.Empty;

    public void Locals() {
        string a = string.Empty;
        string b = String.Empty;
        string c = "";
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
