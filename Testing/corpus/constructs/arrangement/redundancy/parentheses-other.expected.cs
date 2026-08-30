// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
namespace Skala.Corpus.Arrangement;

// dotnet_style_parentheses_in_other_binary_operators, alone on a file that carries no other
// category's case. See redundancy/parentheses-arithmetic.cs for why one category per file: restating
// one of the three keys in the sweep's appended `[*.cs]` section resets the other two to Roslyn's
// defaults, so a fixture holding more than one category answers about the section and not the key.
public class ParenthesesOther {
    // `&&` inside `||`: kept at the export's always_for_clarity, removed at never_if_unnecessary.
    public bool Other(bool a, bool b, bool c) => a || (b && c);
}
