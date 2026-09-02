// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// dotnet_style_parentheses_in_relational_binary_operators, alone on a file that carries no other
// category's case. See redundancy/parentheses-arithmetic.cs for why one category per file: restating
// one of the three keys in the sweep's appended `[*.cs]` section resets the other two to Roslyn's
// defaults, so a fixture holding more than one category answers about the section and not the key.
public class ParenthesesRelational {
    // relational inside relational: kept at always_for_clarity, removed at the export's
    // never_if_unnecessary.
    public bool Relational(int a, int b, int c, int d) => (a > b) == (c > d);
}
